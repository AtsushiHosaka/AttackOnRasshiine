export function deprecatedGameApiResponse() {
  return new Response(
    JSON.stringify({
      Ok: false,
      Error: "deprecated_backend",
      Replacement: "game-api-v2",
    }),
    {
      status: 410,
      headers: {
        "Access-Control-Allow-Origin": "*",
        "Cache-Control": "no-store",
        "Content-Type": "application/json; charset=utf-8",
        "X-Content-Type-Options": "nosniff",
      },
    },
  );
}
