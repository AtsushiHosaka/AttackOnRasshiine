import { deprecatedGameApiResponse } from "../../functions/game-api/handler.ts";

Deno.test("deprecated game-api always fails closed without DB access", async () => {
  const response = deprecatedGameApiResponse();
  const body = await response.json();

  if (response.status !== 410) {
    throw new Error(`expected HTTP 410, got ${response.status}`);
  }
  if (body?.Ok !== false || body?.Error !== "deprecated_backend") {
    throw new Error("deprecated game-api returned an unexpected contract");
  }
  if (body?.Replacement !== "game-api-v2") {
    throw new Error(
      "deprecated game-api must identify the canonical replacement",
    );
  }
});
