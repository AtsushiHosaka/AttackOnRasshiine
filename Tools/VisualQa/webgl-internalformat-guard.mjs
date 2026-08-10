// Unity issue UUM-93245 documents six formats that Chrome incorrectly probes
// with INVALID_ENUM during Unity WebGL startup. This helper is kept standalone
// so the local Visual-QA shell can install the same narrow production guard.
export const unityChromeUnsupportedInternalformats = Object.freeze([
  36756,
  36757,
  36759,
  36760,
  36761,
  36763,
]);

export function installUnityWebGLInternalformatGuard(
  scope = globalThis,
  unsupportedFormats = unityChromeUnsupportedInternalformats,
) {
  const prototype = scope.WebGL2RenderingContext?.prototype;
  const nativeGetInternalformatParameter = prototype?.getInternalformatParameter;
  if (!prototype || typeof nativeGetInternalformatParameter !== "function") {
    return "unavailable";
  }

  const guardMarker = Symbol.for("attack-on-rasshiine.webgl-internalformat-guard");
  if (nativeGetInternalformatParameter[guardMarker] === true) {
    return "already-installed";
  }

  const unsupportedInternalformats = new Set(unsupportedFormats);
  const guardedGetInternalformatParameter = function (target, internalformat, pname) {
    if (unsupportedInternalformats.has(internalformat)) {
      return null;
    }
    return Reflect.apply(nativeGetInternalformatParameter, this, [target, internalformat, pname]);
  };
  Object.defineProperty(guardedGetInternalformatParameter, guardMarker, { value: true });

  try {
    const descriptor = Object.getOwnPropertyDescriptor(
      prototype,
      "getInternalformatParameter",
    );
    Object.defineProperty(prototype, "getInternalformatParameter", {
      configurable: descriptor?.configurable ?? true,
      enumerable: descriptor?.enumerable ?? false,
      writable: descriptor && "writable" in descriptor ? descriptor.writable : true,
      value: guardedGetInternalformatParameter,
    });
  } catch {
    return "unavailable";
  }

  return "installed";
}

export function visualQaWebGLInternalformatGuardSource() {
  return `(${installUnityWebGLInternalformatGuard.toString()})(window,${JSON.stringify(unityChromeUnsupportedInternalformats)});`;
}
