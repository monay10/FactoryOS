import { describe, expect, it, vi } from "vitest";
import { GatewayClient, PERMISSIONS_HEADER, TENANT_HEADER } from "./client";

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

describe("GatewayClient", () => {
  it("sends the tenant header on every call", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ product: "FactoryOS" }));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    await client.system();

    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/system");
    expect((init.headers as Record<string, string>)[TENANT_HEADER]).toBe("acme");
  });

  it("omits the permissions header when unrestricted, and sends it when a set is configured", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({})));

    await new GatewayClient("acme", fetchMock as unknown as typeof fetch).system();
    expect((fetchMock.mock.calls[0][1].headers as Record<string, string>)[PERMISSIONS_HEADER]).toBeUndefined();

    await new GatewayClient("acme", fetchMock as unknown as typeof fetch, ["energy.view", "quality.view"]).system();
    expect((fetchMock.mock.calls[1][1].headers as Record<string, string>)[PERMISSIONS_HEADER]).toBe("energy.view,quality.view");
  });

  it("sends a Bearer token when a session is present, and drops the permissions header", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({})));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch, ["ignored.view"], "jwt-abc");

    await client.system();

    const headers = fetchMock.mock.calls[0][1].headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer jwt-abc");
    expect(headers[PERMISSIONS_HEADER]).toBeUndefined();
  });

  it("throws on a non-2xx response", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response("nope", { status: 400 }));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    await expect(client.shell()).rejects.toThrow(/failed: 400/);
  });

  it("targets the gateway's stable routes", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({})));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    await client.dashboardBoard();
    await client.brainAnswers();
    await client.activityFeed();
    await client.storeCatalog();

    expect(fetchMock.mock.calls.map((c) => c[0])).toEqual([
      "/m/dashboard/board",
      "/m/brain/answers",
      "/m/activity/feed",
      "/store/plugins",
    ]);
  });

  it("POSTs a Brain question as JSON with the tenant header", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() => Promise.resolve(jsonResponse({ tenant: "acme", questionId: "q1", question: "hi" })));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    const accepted = await client.askBrain("Why did press-1 spike?", "user:operator");

    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/m/brain/ask");
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>)[TENANT_HEADER]).toBe("acme");
    expect(JSON.parse(init.body as string)).toEqual({ question: "Why did press-1 spike?", askedBy: "user:operator" });
    expect(accepted.questionId).toBe("q1");
  });

  it("POSTs the enable/disable action with the tenant header", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({ key: "oee", state: "Disabled" })));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    await client.setPluginEnabled("oee", false);

    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/store/plugins/oee/disable");
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>)[TENANT_HEADER]).toBe("acme");
  });

  it("POSTs a work-order close and returns the closed order", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() =>
        Promise.resolve(jsonResponse({ number: "WO-1", title: "Inspect", status: "Closed", assetCode: null, dueAt: null })),
      );
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    const closed = await client.closeWorkOrder("WO-1");

    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/m/maintenance/workorders/WO-1/close");
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>)[TENANT_HEADER]).toBe("acme");
    expect(closed.status).toBe("Closed");
  });

  it("reports a permission error when a close is forbidden", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({}, 403)));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    await expect(client.closeWorkOrder("WO-1")).rejects.toThrow("permission");
  });

  it("POSTs a line quarantine and returns the result", async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() =>
        Promise.resolve(jsonResponse({ tenant: "acme", lineId: "line-1", quarantined: true, newlyQuarantined: true })),
      );
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    const result = await client.quarantineLine("line-1");

    const [path, init] = fetchMock.mock.calls[0];
    expect(path).toBe("/m/quality/lines/line-1/quarantine");
    expect(init.method).toBe("POST");
    expect((init.headers as Record<string, string>)[TENANT_HEADER]).toBe("acme");
    expect(result.newlyQuarantined).toBe(true);
  });

  it("reports a permission error when a quarantine is forbidden", async () => {
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(jsonResponse({}, 403)));
    const client = new GatewayClient("acme", fetchMock as unknown as typeof fetch);

    await expect(client.quarantineLine("line-1")).rejects.toThrow("permission");
  });
});
