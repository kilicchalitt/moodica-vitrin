using bitirme.Services;
using Microsoft.AspNetCore.DataProtection;

namespace bitirme.Tests;

/// <summary>Uzmanın özel notu şifreli saklanır, düz metin okunmaz (ISSUE-22).</summary>
public class SessionNoteProtectorTests
{
    private readonly SessionNoteProtector _protector = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_EncryptsWithPrefix_AndRoundTrips()
    {
        var stored = _protector.Protect("  Uyku hijyeni çalışılacak; ölçek tekrarı.  ");

        Assert.NotNull(stored);
        Assert.StartsWith(SessionNoteProtector.ProtectedPrefix, stored);
        Assert.DoesNotContain("Uyku", stored);
        Assert.Equal("Uyku hijyeni çalışılacak; ölçek tekrarı.", _protector.Unprotect(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Protect_EmptyNote_IsNotStored(string? note)
    {
        Assert.Null(_protector.Protect(note));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Unprotect_MissingNote_IsNull(string? stored)
    {
        Assert.Null(_protector.Unprotect(stored));
    }

    [Fact]
    public void Unprotect_RejectsPlainText()
    {
        Assert.Throws<InvalidOperationException>(() => _protector.Unprotect("düz metin not"));
    }
}
