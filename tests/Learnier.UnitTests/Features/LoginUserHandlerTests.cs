using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Domain.Identity;
using NSubstitute;
using Shouldly;

namespace Learnier.UnitTests.Features;

/// <summary>
/// Giris akisinin karar dallari.
/// </summary>
/// <remarks>
/// Veritabani ve token uretimi taklit ediliyor; buradaki soru "kural dogru mu",
/// "sorgu calisiyor mu" degil. Sorgu tarafi entegrasyon testlerinde dogrulanir.
/// </remarks>
public sealed class LoginUserHandlerTests
{
    private const string Password = "dogru-parola";
    private const string Hash = "ozet";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IRefreshTokenFactory _refreshTokenFactory = Substitute.For<IRefreshTokenFactory>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly LoginUserHandler _handler;

    public LoginUserHandlerTests()
    {
        _tokenService
            .CreateAccessToken(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(new AccessToken("token", DateTimeOffset.UtcNow.AddMinutes(15)));

        _refreshTokenFactory
            .Create()
            .Returns(new NewRefreshToken(
                "yenileme-tokeni",
                "yenileme-ozeti",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(30)));

        _users
            .GetActiveMembershipsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _handler = new LoginUserHandler(
            _users,
            _refreshTokens,
            _refreshTokenFactory,
            _passwordHasher,
            _tokenService,
            _unitOfWork);
    }

    [Fact]
    public async Task ReturnsToken_WhenCredentialsAreValid()
    {
        SetupUser(Active());
        _passwordHasher.Verify(Hash, Password).Returns(PasswordVerificationOutcome.Success);

        var result = await _handler.Handle(new LoginUserCommand("a@b.com", Password), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldBe("token");
        result.Value.User.Email.ShouldBe("a@b.com");
    }

    [Fact]
    public async Task Fails_WhenUserDoesNotExist()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(new LoginUserCommand("yok@b.com", Password), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task PaysHashingCost_WhenUserDoesNotExist()
    {
        // Hesap sayimina karsi: kullanici yoksa da ozetleme yapilmali, aksi halde
        // yanit suresi e-postanin kayitli olup olmadigini ele verir.
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await _handler.Handle(new LoginUserCommand("yok@b.com", Password), TestContext.Current.CancellationToken);

        _passwordHasher.Received(1).Hash(Password);
    }

    [Fact]
    public async Task Fails_WithSameCode_WhenPasswordIsWrong()
    {
        SetupUser(Active());
        _passwordHasher.Verify(Hash, "yanlis").Returns(PasswordVerificationOutcome.Failed);

        var result = await _handler.Handle(new LoginUserCommand("a@b.com", "yanlis"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();

        // Kullanici yok ile parola yanlis ayni kodu dondurmeli.
        result.Error.Code.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Fails_WhenAccountIsSuspended()
    {
        var user = Active();
        user.Suspend();
        SetupUser(user);
        _passwordHasher.Verify(Hash, Password).Returns(PasswordVerificationOutcome.Success);

        var result = await _handler.Handle(new LoginUserCommand("a@b.com", Password), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("auth.account_suspended");
    }

    [Fact]
    public async Task Fails_WhenAccountIsInactive()
    {
        // Davet gibi baska bir akistan bekleyen hesap.
        SetupUser(User.Register("a@b.com", "Ad", "Soyad", Hash));
        _passwordHasher.Verify(Hash, Password).Returns(PasswordVerificationOutcome.Success);

        var result = await _handler.Handle(new LoginUserCommand("a@b.com", Password), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("auth.account_inactive");
    }

    [Fact]
    public async Task ChecksPasswordBeforeStatus()
    {
        // Askidaki hesaba yanlis parolayla girilirse hesabin askida oldugu
        // sizdirilmamali; donen kod kimlik bilgisi hatasi olmali.
        var user = Active();
        user.Suspend();
        SetupUser(user);
        _passwordHasher.Verify(Hash, "yanlis").Returns(PasswordVerificationOutcome.Failed);

        var result = await _handler.Handle(new LoginUserCommand("a@b.com", "yanlis"), TestContext.Current.CancellationToken);

        result.Error!.Code.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task RehashesPassword_WhenHashIsOutdated()
    {
        SetupUser(Active());
        _passwordHasher.Verify(Hash, Password).Returns(PasswordVerificationOutcome.SuccessRehashNeeded);
        _passwordHasher.Hash(Password).Returns("yeni-ozet");

        var result = await _handler.Handle(new LoginUserCommand("a@b.com", Password), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ozet gunceldeyken parola yeniden ozetlenmemeli.
    /// </summary>
    /// <remarks>
    /// Bu test eskiden "hic kayit yapilmamali" diyordu. Artik her basarili giriste
    /// yenileme tokeni yazildigi icin kayit her zaman yapiliyor; korunmak istenen
    /// asil davranis ise gereksiz yeniden ozetlemenin olmamasiydi.
    /// </remarks>
    [Fact]
    public async Task DoesNotRehashPassword_WhenHashIsCurrent()
    {
        SetupUser(Active());
        _passwordHasher.Verify(Hash, Password).Returns(PasswordVerificationOutcome.Success);

        await _handler.Handle(new LoginUserCommand("a@b.com", Password), TestContext.Current.CancellationToken);

        _passwordHasher.DidNotReceive().Hash(Arg.Any<string>());
    }

    [Fact]
    public async Task IssuesRefreshToken_WhenCredentialsAreValid()
    {
        SetupUser(Active());
        _passwordHasher.Verify(Hash, Password).Returns(PasswordVerificationOutcome.Success);

        var result = await _handler.Handle(new LoginUserCommand("a@b.com", Password), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();

        // Yanitta ham token doner, veritabanina yalnizca ozeti yazilir.
        result.Value.RefreshToken.ShouldBe("yenileme-tokeni");
        _refreshTokens.Received(1).Add(Arg.Is<RefreshToken>(t => t.TokenHash == "yenileme-ozeti"));
    }

    private static User Active()
    {
        var user = User.Register("a@b.com", "Ad", "Soyad", Hash);
        user.ConfirmEmail(DateTimeOffset.UtcNow);
        return user;
    }

    private void SetupUser(User user)
        => _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
}

