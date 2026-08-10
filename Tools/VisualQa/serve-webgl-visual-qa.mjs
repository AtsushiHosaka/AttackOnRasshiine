import { createServer } from "node:http";
import { readFile, stat } from "node:fs/promises";
import { dirname, extname, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { visualQaWebGLInternalformatGuardSource } from "./webgl-internalformat-guard.mjs";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDirectory, "../..");
const buildRoot = resolve(projectRoot, "Builds/WebGLVisualQa");
const unityBuildPrefix = "WebGLVisualQa";
const port = parsePort(process.argv.slice(2));
const loopbackHosts = new Set(["localhost", "127.0.0.1", "[::1]", "::1"]);

await assertBuildExists();
const immutableBuildId = await readImmutableBuildId();

const server = createServer(async (request, response) => {
  applySecurityHeaders(response);
  if (!isLoopbackHostHeader(request.headers.host)) {
    sendText(response, 403, "Loopback host required.");
    return;
  }

  if (request.method !== "GET" && request.method !== "HEAD") {
    response.setHeader("Allow", "GET, HEAD");
    sendText(response, 405, "Method not allowed.");
    return;
  }

  let pathname;
  try {
    pathname = decodeURIComponent(new URL(request.url ?? "/", "http://localhost").pathname);
  } catch {
    sendText(response, 400, "Invalid URL.");
    return;
  }

  if (pathname === "/" || pathname === "/index.html") {
    sendBuffer(response, request.method, 200, Buffer.from(visualQaHtml()), "text/html; charset=utf-8");
    return;
  }

  const filePath = resolve(buildRoot, `.${pathname}`);
  if (filePath !== buildRoot && !filePath.startsWith(`${buildRoot}${sep}`)) {
    sendText(response, 403, "Path rejected.");
    return;
  }

  try {
    const fileStat = await stat(filePath);
    if (!fileStat.isFile()) {
      sendText(response, 404, "Not found.");
      return;
    }

    const body = await readFile(filePath);
    const headers = artifactHeaders(filePath);
    for (const [name, value] of Object.entries(headers)) {
      response.setHeader(name, value);
    }
    sendBuffer(response, request.method, 200, body, headers["Content-Type"]);
  } catch {
    sendText(response, 404, "Not found.");
  }
});

server.listen(port, "127.0.0.1", () => {
  process.stdout.write(`AttackOnRasshiine visual QA: http://127.0.0.1:${port}/\n`);
  process.stdout.write("This server accepts loopback requests only and never contacts Supabase.\n");
});

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.on(signal, () => server.close(() => process.exit(0)));
}

function parsePort(args) {
  const portIndex = args.indexOf("--port");
  const value = portIndex >= 0 ? Number(args[portIndex + 1]) : 4190;
  if (!Number.isInteger(value) || value < 1024 || value > 65535) {
    throw new Error("--port must be an integer from 1024 through 65535.");
  }
  return value;
}

async function assertBuildExists() {
  for (const path of [
    `Build/${unityBuildPrefix}.loader.js`,
    `Build/${unityBuildPrefix}.data`,
    `Build/${unityBuildPrefix}.framework.js`,
    `Build/${unityBuildPrefix}.wasm`,
  ]) {
    const fileStat = await stat(resolve(buildRoot, path));
    if (!fileStat.isFile()) {
      throw new Error(`Visual-QA artifact is incomplete: ${path}`);
    }
  }
}

async function readImmutableBuildId() {
  const wasmStat = await stat(resolve(buildRoot, `Build/${unityBuildPrefix}.wasm`));
  return `${wasmStat.size}-${Math.floor(wasmStat.mtimeMs)}`;
}

function isLoopbackHostHeader(value) {
  if (!value) return false;
  try {
    const hostname = new URL(`http://${value}`).hostname.toLowerCase();
    return loopbackHosts.has(hostname);
  } catch {
    return false;
  }
}

function applySecurityHeaders(response) {
  response.setHeader("Content-Security-Policy", "default-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; script-src 'self' 'unsafe-inline' 'unsafe-eval' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; connect-src 'self'; worker-src 'self' blob:");
  response.setHeader("Cross-Origin-Opener-Policy", "same-origin");
  response.setHeader("Cross-Origin-Resource-Policy", "same-origin");
  response.setHeader("Referrer-Policy", "no-referrer");
  response.setHeader("X-Content-Type-Options", "nosniff");
  response.setHeader("X-Frame-Options", "DENY");
  response.setHeader("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=(), serial=()");
  response.setHeader("Cache-Control", "no-store, max-age=0");
}

function artifactHeaders(path) {
  const stableAsset = path.includes(`${sep}Build${sep}`) || path.includes(`${sep}TemplateData${sep}`);
  const cacheHeaders = {
    "Cache-Control": stableAsset
      ? "public, max-age=31536000, immutable"
      : "no-store, max-age=0",
  };
  if (path.endsWith(".wasm.br")) {
    return { ...cacheHeaders, "Content-Type": "application/wasm", "Content-Encoding": "br" };
  }
  if (path.endsWith(".js.br")) {
    return { ...cacheHeaders, "Content-Type": "application/javascript; charset=utf-8", "Content-Encoding": "br" };
  }
  if (path.endsWith(".data.br")) {
    return { ...cacheHeaders, "Content-Type": "application/octet-stream", "Content-Encoding": "br" };
  }
  if (path.endsWith(".wasm")) {
    return { ...cacheHeaders, "Content-Type": "application/wasm" };
  }
  if (path.endsWith(".data")) {
    return { ...cacheHeaders, "Content-Type": "application/octet-stream" };
  }
  if (path.endsWith(".js")) {
    return { ...cacheHeaders, "Content-Type": "application/javascript; charset=utf-8" };
  }
  if (path.endsWith(".json")) {
    return { ...cacheHeaders, "Content-Type": "application/json; charset=utf-8" };
  }
  const extension = extname(path).toLowerCase();
  if (extension === ".css") return { ...cacheHeaders, "Content-Type": "text/css; charset=utf-8" };
  if (extension === ".png") return { ...cacheHeaders, "Content-Type": "image/png" };
  if (extension === ".ico") return { ...cacheHeaders, "Content-Type": "image/x-icon" };
  return { ...cacheHeaders, "Content-Type": "application/octet-stream" };
}

function sendText(response, statusCode, value) {
  sendBuffer(response, "GET", statusCode, Buffer.from(value), "text/plain; charset=utf-8");
}

function sendBuffer(response, method, statusCode, body, contentType) {
  response.statusCode = statusCode;
  response.setHeader("Content-Type", contentType);
  response.setHeader("Content-Length", String(body.byteLength));
  response.end(method === "HEAD" ? undefined : body);
}

function visualQaHtml() {
  return `<!doctype html>
<html lang="ja">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,height=device-height,initial-scale=1,maximum-scale=1,user-scalable=no">
  <link rel="icon" href="/TemplateData/favicon.ico">
  <title>AttackOnRasshiine — Local Visual QA</title>
  <style>
    html,body,#qa-shell,#unity-canvas{width:100%;height:100%;margin:0;overflow:hidden;background:#07162c}
    #unity-canvas{display:block;outline:none}
    #qa-loading{position:fixed;inset:0;display:grid;place-items:center;color:#f7e4ae;font:600 16px system-ui;background:#07162c;letter-spacing:.08em}
    #qa-shell[data-shell-state="ready"] #qa-loading{display:none}
  </style>
</head>
<body>
  <main id="qa-shell" data-shell-state="loading">
    <canvas id="unity-canvas" tabindex="-1"></canvas>
    <div id="qa-loading">LOCAL VISUAL QA — UNITYを読み込んでいます</div>
  </main>
  <script>
    ${visualQaWebGLInternalformatGuardSource()}
    const shell=document.querySelector('#qa-shell');
    const canvas=document.querySelector('#unity-canvas');
    const buildId='${immutableBuildId}';
    const buildUrl=path=>path+'?build='+encodeURIComponent(buildId);
    const qaCachePrefix='aor-visual-qa-artifact-';
    const qaCacheName=qaCachePrefix+buildId;
    const qaCachePromise=caches.open(qaCacheName);
    caches.keys().then(keys=>Promise.all(keys.filter(key=>key.startsWith(qaCachePrefix)&&key!==qaCacheName).map(key=>caches.delete(key))));
    const nativeFetch=window.fetch.bind(window);
    window.fetch=async(input,init)=>{
      const request=input instanceof Request ? new Request(input,init) : new Request(input,init);
      const url=new URL(request.url);
      if(request.method==='GET'&&url.origin===location.origin&&url.pathname==='/Build/${unityBuildPrefix}.wasm'){
        const cache=await qaCachePromise;
        const cached=await cache.match(request);
        if(cached){console.log('[AOR_QA_CACHE] wasm hit '+buildId);return cached}
        const response=await nativeFetch(request);
        if(response.ok){
          try{await cache.put(request,response.clone());console.log('[AOR_QA_CACHE] wasm stored '+buildId)}
          catch(error){console.warn('[AOR_QA_CACHE] wasm store failed',error)}
        }
        return response;
      }
      return nativeFetch(request);
    };
    const script=document.createElement('script');
    script.src=buildUrl('/Build/${unityBuildPrefix}.loader.js');
    script.onload=async()=>{
      try{
        window.__AOR_QA_UNITY__=await createUnityInstance(canvas,{
          dataUrl:buildUrl('/Build/${unityBuildPrefix}.data'),
          frameworkUrl:buildUrl('/Build/${unityBuildPrefix}.framework.js'),
          codeUrl:buildUrl('/Build/${unityBuildPrefix}.wasm'),
          streamingAssetsUrl:'/StreamingAssets',
          companyName:'AtsushiHosaka',
          productName:'AttackOnRasshiine Visual QA',
          productVersion:'local-development',
          cacheControl:()=> 'immutable',
          devicePixelRatio:Math.min(1.5,Math.max(.75,window.devicePixelRatio||1)),
          showBanner:(message,type)=>console[type==='error'?'error':'warn'](message)
        });
        shell.dataset.shellState='ready';
        canvas.focus({preventScroll:true});
      }catch(error){shell.dataset.shellState='error';console.error(error)}
    };
    script.onerror=()=>{shell.dataset.shellState='error';console.error('unity_loader_failed')};
    document.head.appendChild(script);
  </script>
</body>
</html>`;
}
