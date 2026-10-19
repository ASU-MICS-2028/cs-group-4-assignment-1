using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Security;
using Xunit.Abstractions;

namespace StudentPortal.Tests;

// The T1-T8 security test matrix from the assignment brief, run against the real API.
// Each test writes one evidence line: test | identity | request | expected | observed.
[Collection(PortalCollection.Name)]
public sealed class SecurityMatrixTests(PortalFixture portal, ITestOutputHelper output)
{
    private void Evidence(string test, string identity, string request, string expected, HttpResponseMessage response) =>
        output.WriteLine($"{test} | {identity} | {request} | expected {expected} | observed {(int)response.StatusCode} {response.StatusCode}");

    [Fact]
    public async Task T1_S001_registers_a_passkey_and_the_public_key_is_stored_against_S001()
    {
        var browser = portal.NewBrowser();
        var (passkey, response) = await portal.EnrollAsync(browser, "S001");
        Evidence("T1", "S001", "POST /api/enrollment/complete", "201", response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = await portal.WithDbAsync(db => db.PasskeyCredentials.SingleAsync(c => c.CredentialId == passkey.CredentialId));
        Assert.Equal("S001", stored.AccountId);
        Assert.NotEmpty(stored.PublicKey);
        output.WriteLine($"   credential bound to {stored.AccountId}; public key {stored.PublicKey.Length} bytes stored server-side");
    }

    [Fact]
    public async Task T1b_enrollment_with_a_wrong_code_is_rejected_and_no_challenge_is_issued()
    {
        await portal.IssueEnrollmentCodeAsync("S002");
        var response = await portal.NewBrowser().PostAsJsonAsync("/api/enrollment/begin", new { accountId = "S002", code = "AAAA-BBBB-CCCC" });
        Evidence("T1b", "anonymous", "POST /api/enrollment/begin (wrong code for S002)", "400", response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task T1c_an_enrollment_code_cannot_be_used_twice()
    {
        var code = await portal.IssueEnrollmentCodeAsync("S002");
        var browser = portal.NewBrowser();
        var begin = await browser.PostAsJsonAsync("/api/enrollment/begin", new { accountId = "S002", code });
        using var passkey = new SoftwarePasskey(PortalFixture.RpId, PortalFixture.Origin);
        await browser.PostAsJsonAsync("/api/enrollment/complete", passkey.Register(await begin.Content.ReadAsStringAsync()));

        var reuse = await portal.NewBrowser().PostAsJsonAsync("/api/enrollment/begin", new { accountId = "S002", code });
        Evidence("T1c", "anonymous", "POST /api/enrollment/begin (reused code)", "400", reuse);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
    }

    [Fact]
    public async Task T2_S001_signs_in_with_the_registered_passkey_and_gets_a_session()
    {
        var browser = portal.NewBrowser();
        var (passkey, _) = await portal.EnrollAsync(browser, "S001");

        var signIn = await PortalFixture.SignInAsync(browser, passkey);
        Evidence("T2", "S001", "POST /api/auth/verify (fresh challenge)", "200 + session cookie", signIn);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Contains(signIn.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SessionService.CookieName) && c.Contains("httponly") && c.Contains("secure") && c.Contains("samesite=strict"));

        var me = await browser.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal("S001", (string?)me!["accountId"]);
        output.WriteLine("   GET /api/auth/me -> S001 (student)");
    }

    [Fact]
    public async Task T3a_altered_signature_is_rejected_and_no_session_is_created()
    {
        var browser = portal.NewBrowser();
        var (passkey, _) = await portal.EnrollAsync(browser, "S001");

        var options = await browser.PostAsync("/api/auth/options", null);
        var assertion = passkey.SignIn(await options.Content.ReadAsStringAsync());
        var signature = Base64Url.DecodeFromChars((string)assertion["response"]!["signature"]!);
        signature[^1] ^= 0xFF;
        assertion["response"]!["signature"] = Base64Url.EncodeToString(signature);

        var response = await browser.PostAsJsonAsync("/api/auth/verify", assertion);
        Evidence("T3a", "S001 passkey", "POST /api/auth/verify (one signature byte flipped)", "401", response);
        await AssertNoSessionAsync(browser, response);
    }

    [Fact]
    public async Task T3b_replaying_a_captured_response_is_rejected()
    {
        var setup = portal.NewBrowser();
        var (passkey, _) = await portal.EnrollAsync(setup, "S001");

        // A client that does not manage cookies, so the attacker's request is fully under test control.
        var attacker = portal.CreateClient(new() { BaseAddress = new Uri(PortalFixture.Origin), HandleCookies = false });
        var options = await attacker.PostAsync("/api/auth/options", null);
        var ceremonyCookie = CookieValue(options, CeremonyStore.CookieName);
        var assertion = passkey.SignIn(await options.Content.ReadAsStringAsync());

        var first = await PostWithCookie(attacker, "/api/auth/verify", assertion, CeremonyStore.CookieName, ceremonyCookie);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await PostWithCookie(attacker, "/api/auth/verify", assertion, CeremonyStore.CookieName, ceremonyCookie);
        Evidence("T3b", "captured S001 response", "POST /api/auth/verify (same response + same challenge, sent again)", "401", replay);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.DoesNotContain(SetCookies(replay), c => c.StartsWith(SessionService.CookieName + "="));
    }

    [Fact]
    public async Task T3c_old_response_submitted_against_a_new_challenge_is_rejected()
    {
        var browser = portal.NewBrowser();
        var (passkey, _) = await portal.EnrollAsync(browser, "S001");

        var oldOptions = await browser.PostAsync("/api/auth/options", null);
        var oldAssertion = passkey.SignIn(await oldOptions.Content.ReadAsStringAsync());

        await browser.PostAsync("/api/auth/options", null);
        var response = await browser.PostAsJsonAsync("/api/auth/verify", oldAssertion);
        Evidence("T3c", "S001 passkey", "POST /api/auth/verify (response signed over an earlier challenge)", "401", response);
        await AssertNoSessionAsync(browser, response);
    }

    [Fact]
    public async Task T3d_response_created_for_another_origin_is_rejected()
    {
        var browser = portal.NewBrowser();
        var (passkey, _) = await portal.EnrollAsync(browser, "S001");

        var options = await browser.PostAsync("/api/auth/options", null);
        var assertion = passkey.SignIn(await options.Content.ReadAsStringAsync(), originOverride: "https://portal-login.example");

        var response = await browser.PostAsJsonAsync("/api/auth/verify", assertion);
        Evidence("T3d", "S001 passkey", "POST /api/auth/verify (clientData origin = phishing site)", "401", response);
        await AssertNoSessionAsync(browser, response);
    }

    [Fact]
    public async Task T4_protected_record_without_a_session_is_denied()
    {
        var response = await portal.NewBrowser().GetAsync("/api/students/S001");
        Evidence("T4", "unauthenticated", "GET /api/students/S001", "401", response);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var grade = await portal.NewBrowser().PostAsJsonAsync("/api/courses/C101/grades/S001", new { score = 100 });
        Evidence("T4", "unauthenticated", "POST /api/courses/C101/grades/S001", "401", grade);
        Assert.Equal(HttpStatusCode.Unauthorized, grade.StatusCode);
    }

    [Fact]
    public async Task T5_S001_can_read_own_record()
    {
        var s001 = await portal.SignedInAsync("S001");
        var response = await s001.GetAsync("/api/students/S001");
        Evidence("T5", "S001", "GET /api/students/S001", "200", response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("S001", (string?)record!["studentId"]);
    }

    [Fact]
    public async Task T6_S001_cannot_read_S002_by_changing_the_id()
    {
        var s001 = await portal.SignedInAsync("S001");

        var response = await s001.GetAsync("/api/students/S002");
        Evidence("T6", "S001", "GET /api/students/S002", "403", response);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Kwame", await response.Content.ReadAsStringAsync());

        var lowercase = await s001.GetAsync("/api/students/s002");
        Evidence("T6", "S001", "GET /api/students/s002 (case variation)", "403", lowercase);
        Assert.Equal(HttpStatusCode.Forbidden, lowercase.StatusCode);
    }

    [Fact]
    public async Task T7_S001_calling_the_grade_update_endpoint_is_denied_and_the_grade_is_unchanged()
    {
        var before = await ScoreAsync("C101", "S001");
        var s001 = await portal.SignedInAsync("S001");

        var response = await s001.PostAsJsonAsync("/api/courses/C101/grades/S001", new { score = 100 });
        Evidence("T7", "S001", "POST /api/courses/C101/grades/S001 {score:100}", "403", response);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var after = await ScoreAsync("C101", "S001");
        Assert.Equal(before, after);
        output.WriteLine($"   C101/S001 score before {before}, after {after} (unchanged)");
    }

    [Fact]
    public async Task T8_L001_updates_a_grade_in_assigned_course_C101()
    {
        var l001 = await portal.SignedInAsync("L001");
        var before = await ScoreAsync("C101", "S002");
        var newScore = before == 82 ? 83 : 82;

        var response = await l001.PostAsJsonAsync("/api/courses/C101/grades/S002", new { score = newScore });
        Evidence("T8", "L001", $"POST /api/courses/C101/grades/S002 {{score:{newScore}}}", "200", response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(newScore, await ScoreAsync("C101", "S002"));

        var audit = await portal.WithDbAsync(db => db.AuditEvents.OrderByDescending(a => a.Id).FirstAsync(a => a.Event == "grade.updated"));
        Assert.Equal("L001", audit.ActorId);
        output.WriteLine($"   C101/S002 score {before} -> {newScore}; audit: {audit.ActorId} {audit.Event} {audit.Target} {audit.Detail}");
    }

    [Fact]
    public async Task T8b_L001_cannot_update_grades_or_read_students_outside_C101()
    {
        var l001 = await portal.SignedInAsync("L001");

        var otherCourse = await l001.PostAsJsonAsync("/api/courses/C102/grades/S003", new { score = 10 });
        Evidence("T8b", "L001", "POST /api/courses/C102/grades/S003 (course L001 does not teach)", "403", otherCourse);
        Assert.Equal(HttpStatusCode.Forbidden, otherCourse.StatusCode);

        var notEnrolled = await l001.PostAsJsonAsync("/api/courses/C101/grades/S003", new { score = 10 });
        Evidence("T8b", "L001", "POST /api/courses/C101/grades/S003 (student not in C101)", "403", notEnrolled);
        Assert.Equal(HttpStatusCode.Forbidden, notEnrolled.StatusCode);

        var otherStudent = await l001.GetAsync("/api/students/S003");
        Evidence("T8b", "L001", "GET /api/students/S003 (not in any L001 course)", "403", otherStudent);
        Assert.Equal(HttpStatusCode.Forbidden, otherStudent.StatusCode);

        var shared = await l001.GetFromJsonAsync<JsonObject>("/api/students/S001");
        var courses = shared!["courses"]!.AsArray().Select(c => (string?)c!["courseId"]).ToList();
        Assert.Equal(new string?[] { "C101" }, courses);
        output.WriteLine("   GET /api/students/S001 as L001 -> 200, shows C101 only (S001's C102 result hidden)");
    }

    [Fact]
    public async Task T8c_lecturer_loses_access_when_the_course_assignment_has_ended()
    {
        await portal.WithDbAsync(async db =>
        {
            db.CourseAssignments.Add(new() { LecturerId = "L002", CourseId = "C101", StartsOn = new DateOnly(2026, 1, 5), EndsOn = new DateOnly(2026, 5, 1) });
            return await db.SaveChangesAsync();
        });

        var l002 = await portal.SignedInAsync("L002");
        var response = await l002.PostAsJsonAsync("/api/courses/C101/grades/S001", new { score = 50 });
        Evidence("T8c", "L002", "POST /api/courses/C101/grades/S001 (assignment ended 2026-05-01)", "403", response);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_invalidates_the_session_even_if_the_cookie_is_replayed()
    {
        var browser = portal.NewBrowser();
        var (passkey, _) = await portal.EnrollAsync(browser, "S001");

        var cookieJar = portal.CreateClient(new() { BaseAddress = new Uri(PortalFixture.Origin), HandleCookies = false });
        var signIn = await PortalFixture.SignInAsync(browser, passkey);
        var sessionCookie = CookieValue(signIn, SessionService.CookieName);

        var logout = await browser.PostAsync("/api/auth/logout", null);
        Evidence("Logout", "S001", "POST /api/auth/logout", "204", logout);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var afterLogout = await browser.GetAsync("/api/students/S001");
        Evidence("Logout", "S001 (logged out)", "GET /api/students/S001", "401", afterLogout);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/students/S001");
        request.Headers.Add("Cookie", $"{SessionService.CookieName}={sessionCookie}");
        var replayed = await cookieJar.SendAsync(request);
        Evidence("Logout", "copied pre-logout cookie", "GET /api/students/S001", "401", replayed);
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    private Task<int?> ScoreAsync(string courseId, string studentId) =>
        portal.WithDbAsync(db => db.CourseEnrollments.Where(e => e.CourseId == courseId && e.StudentId == studentId).Select(e => e.Score).SingleAsync());

    private static async Task AssertNoSessionAsync(HttpClient browser, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    private static string CookieValue(HttpResponseMessage response, string name) =>
        SetCookies(response).Select(c => c.Split(';')[0]).Single(c => c.StartsWith(name + "="))[(name.Length + 1)..];

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string url, JsonObject body, string cookieName, string cookieValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Cookie", $"{cookieName}={cookieValue}");
        return client.SendAsync(request);
    }
}
