using System.Net;
using System.Text.Json;
using DaycareApi.Tests.Fixtures;
using DaycareApi.Tests.Helpers;

namespace DaycareApi.Tests.Features;

[Collection(IntegrationCollection.Name)]
public sealed class ReportPermissionTests(ApiFactory factory)
{
    [Fact]
    public async Task Parent_without_CanViewReports_cannot_read_published_reports()
    {
        var admin = new ApiClient(factory.CreateClient());
        await admin.LoginAs("admin@demo.local", "Admin123!");
        var children = await admin.GetJson("/api/children");
        var child = children.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"reports-{suffix}@test.local";

        var guardian = await admin.Post("/api/guardians", new
        {
            fullName = "No reports guardian", email, password = "Parent123!",
            childId = child, relationship = "Parent", isPrimary = false,
            canPickup = true, canViewReports = false
        });
        guardian.EnsureSuccessStatusCode();

        var entry = await admin.Post("/api/reports/entries", new
        {
            childId = child, date = "2026-10-08", entryType = "note", details = "Private test"
        });
        entry.EnsureSuccessStatusCode();
        var reportId = JsonDocument.Parse(await entry.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();
        var publish = await admin.Put($"/api/reports/{reportId}",
            new { generalNote = "Published test", publish = true });
        publish.EnsureSuccessStatusCode();

        var parent = new ApiClient(factory.CreateClient());
        await parent.LoginAs(email, "Parent123!");
        var reports = await parent.GetJson($"/api/reports?childId={child}");
        Assert.Empty(reports.RootElement.EnumerateArray());
    }
}
