namespace TestService.Unit;

[TestFixture]
public class LoginResultTests
{
    [Test]
    public void Success_IsSuccessTrue()
    {
        var result = LoginResult.Success(new LoginResponse { Token = "tok" });

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Success_ContainsResponse()
    {
        var response = new LoginResponse { Token = "tok", Username = "alice" };

        var result = LoginResult.Success(response);

        Assert.That(result.Response, Is.SameAs(response));
        Assert.That(result.FailureReason, Is.Null);
    }

    [Test]
    public void Fail_IsSuccessFalse()
    {
        var result = LoginResult.Fail(LoginFailureReason.InvalidPassword);

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public void Fail_ContainsReason()
    {
        var result = LoginResult.Fail(LoginFailureReason.UserNotFoundOrInactive);

        Assert.That(result.FailureReason, Is.EqualTo(LoginFailureReason.UserNotFoundOrInactive));
        Assert.That(result.Response, Is.Null);
    }
}
