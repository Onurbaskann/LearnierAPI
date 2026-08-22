namespace Learnier.Application.Common.Abstractions;

/// <summary>Yapilandirilmis varsayilan saglayiciyi veya ada gore bir adaptoru cozer.</summary>
public interface IPaymentProviderResolver
{
    IPaymentProvider DefaultProvider { get; }

    IPaymentProvider? Find(string providerName);
}
