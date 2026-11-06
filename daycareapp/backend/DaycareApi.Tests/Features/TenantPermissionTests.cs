using System.Net;
using System.Text.Json;
using DaycareApi.Tests.Fixtures;
using DaycareApi.Tests.Helpers;

namespace DaycareApi.Tests.Features;

[Collection(IntegrationCollection.Name)]
public sealed class TenantPermissionTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_cannot_read_or_use_another_daycares_classroom()
    {
        var super = new ApiClient(factory.CreateClient());
        await super.LoginAs("super@admin.local", "123456");

        var suffix = Guid.NewGuid().ToString("N");
        var createCenter = await super.Post("/api/centers", new
        {
            name = $"Isolation test {suffix}",
            adminName = "Isolation Admin",
            adminEmail = $"admin-{suffix}@test.local",
            adminPassword = "123456"
        });
        createCenter.EnsureSuccessStatusCode();

        var other = new ApiClient(factory.CreateClient());
        await other.LoginAs($"admin-{suffix}@test.local", "123456");
        var createRoom = await other.Post("/api/classrooms", new
        {
            name = $"Private room {suffix}", ageGroup = "2-3 years", capacity = 8
        });
        createRoom.EnsureSuccessStatusCode();
        var room = JsonDocument.Parse(await createRoom.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();

        var admin = new ApiClient(factory.CreateClient());
        await admin.LoginAs("admin@demo.local", "123456");
        var classrooms = await admin.Http.GetAsync("/api/classrooms");
        Assert.Equal(HttpStatusCode.OK, classrooms.StatusCode);
        Assert.DoesNotContain(room.ToString(), await classrooms.Content.ReadAsStringAsync());

        var crossCenterCreate = await admin.Post("/api/children", new
        {
            firstName = "Should", lastName = "Fail", dateOfBirth = "2023-01-01",
            classroomId = room
        });
        Assert.Equal(HttpStatusCode.BadRequest, crossCenterCreate.StatusCode);
    }
}
