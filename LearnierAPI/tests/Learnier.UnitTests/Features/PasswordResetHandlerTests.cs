using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Authentication.Commands.RequestPasswordReset;
using Learnier.Application.Features.Authentication.Commands.ResetPassword;
using Learnier.Domain.Identity;
using NSubstitute;
using Shouldly;

namespace Learnier.UnitTests.Features;

public sealed class PasswordResetHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenStore _tokens = Substitute.For<IPasswordResetTokenStore>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();

    [Fact]
    public async Task Request_DoesNotRevealUnknownEmail()
    {
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new RequestPasswordResetHandler(_users, _tokens, _emailSender);
        var result = await handler.Handle(
            new RequestPasswordResetCommand("yok@ornek.com"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _tokens.DidNotReceive().Issue(Arg.Any<Guid>());
        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<EmailNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Request_IssuesTokenAndSendsNotification()
    {
        var user = ActiveUser();
        _users.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _tokens.Issue(user.Id).Returns(new NewPasswordResetToken(
            "ham-token",
            DateTimeOffset.UtcNow.AddMinutes(30)));

        var handler = new RequestPasswordResetHandler(_users, _tokens, _emailSender);
        var result = await handler.Handle(
            new RequestPasswordResetCommand(user.Email),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailNotification>(notification =>
                notification.TemplateCode == "email.password_reset"
                && notification.Parameters["token"] == "ham-token"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_RejectsInvalidToken()
    {
        _tokens.Consume("gecersiz").Returns((Guid?)null);

        var handler = CreateResetHandler(out _, out _, out _);
        var result = await handler.Handle(
            new ResetPasswordCommand("gecersiz", "YeniParola123"),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("auth.invalid_password_reset_token");
    }

    [Fact]
    public async Task Reset_ChangesPasswordAndRevokesActiveSessions()
    {
        var user = ActiveUser();
        var now = DateTimeOffset.UtcNow;
        var refreshToken = RefreshToken.Issue(user.Id, "ozet", now, now.AddDays(1));

        _tokens.Consume("gecerli").Returns(user.Id);
        _users.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var handler = CreateResetHandler(out var refreshTokens, out var passwordHasher, out var unitOfWork);
        refreshTokens.FindActiveByUserIdAsync(
                user.Id,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns([refreshToken]);
        passwordHasher.Hash("YeniParola123").Returns("yeni-ozet");

        var result = await handler.Handle(
            new ResetPasswordCommand("gecerli", "YeniParola123"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe("yeni-ozet");
        refreshToken.RevokedAt.ShouldNotBeNull();
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ResetPasswordHandler CreateResetHandler(
        out IRefreshTokenRepository refreshTokens,
        out IPasswordHasher passwordHasher,
        out IUnitOfWork unitOfWork)
    {
        refreshTokens = Substitute.For<IRefreshTokenRepository>();
        passwordHasher = Substitute.For<IPasswordHasher>();
        unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        return new ResetPasswordHandler(
            _tokens,
            _users,
            refreshTokens,
            passwordHasher,
            unitOfWork,
            clock);
    }

    private static User ActiveUser()
    {
        var user = User.Register("test@ornek.com", "Test", "Kullanici", "eski-ozet");
        user.ConfirmEmail(DateTimeOffset.UtcNow);
        return user;
    }
}
