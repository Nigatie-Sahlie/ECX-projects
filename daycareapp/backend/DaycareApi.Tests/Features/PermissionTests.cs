using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using DaycareApi.Tests.Fixtures;
using DaycareApi.Tests.Helpers;

[Collection(IntegrationCollection.Name)]
public sealed class PermissionTests(ApiFactory factory)
{
    [Fact]
    public async Task Anonymous_user_cannot_read_children()
    {
        var response = await factory.CreateClient().GetAsync("/api/children");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Parent_cannot_create_classrooms()
    {
        var client = new ApiClient(factory.CreateClient());
        await client.LoginAs("parent@demo.local", "Parent123!");

        var response = await client.Post("/api/classrooms", new
        {
            name = "Not allowed",
            ageGroup = "2-3 years",
            capacity = 10
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_can_read_assigned_classrooms_but_cannot_create_them()
    {
        var client = new ApiClient(factory.CreateClient());
        await client.LoginAs("staff@demo.local", "Staff123!");

        var read = await client.Http.GetAsync("/api/classrooms");
        var create = await client.Post("/api/classrooms", new
        {
            name = "Not allowed",
            ageGroup = "2-3 years",
            capacity = 10
        });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }
}
