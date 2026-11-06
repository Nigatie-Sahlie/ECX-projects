using System.Net;
using System.Text.Json;
using DaycareApi.Tests.Fixtures;
using DaycareApi.Tests.Helpers;

namespace DaycareApi.Tests.Features;

[Collection(IntegrationCollection.Name)]
public sealed class AnnouncementPermissionTests(ApiFactory factory)
{
    [Fact]
    public async Task Staff_can_post_only_to_an_assigned_classroom()
    {
        var admin = new ApiClient(factory.CreateClient());
        await admin.LoginAs("admin@demo.local", "123456");
        var rooms = await admin.GetJson("/api/classrooms");
        var assignedRoom = rooms.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        var otherRoomResponse = await admin.Post("/api/classrooms",
            new { name = $"Unassigned {Guid.NewGuid():N}", ageGroup = "4-5 years", capacity = 8 });
        otherRoomResponse.EnsureSuccessStatusCode();
        var otherRoom = (await JsonDocument.ParseAsync(await otherRoomResponse.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        var staff = new ApiClient(factory.CreateClient());
        await staff.LoginAs("staff@demo.local", "123456");
        var allowed = await staff.Post("/api/announcements",
            new { title = "Room update", body = "Today", classroomId = assignedRoom });
        var wrongRoom = await staff.Post("/api/announcements",
            new { title = "Wrong room", body = "No", classroomId = otherRoom });
        var centerWide = await staff.Post("/api/announcements",
            new { title = "Center update", body = "No", classroomId = (Guid?)null });

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRoom.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, centerWide.StatusCode);
    }
}
