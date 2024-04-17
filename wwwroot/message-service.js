function registerService(monacoService) {
  const methods = {};

  // Override the invokeMethod prototype
  // Biggest bottleneck here is marshalling JS to WASM for strings
  // So we cut out the middleman and intercept this in our message listener and save one round trip
  const orig = monacoService.__proto__.invokeMethod;
  monacoService.__proto__.invokeMethod = function (...args) {
    try {
      const parsed = JSON.parse(args[args.length - 1]);
      const parsedResult = JSON.parse(
        atob(parsed.ResultPayload.slice(1, parsed.ResultPayload.length - 1))
      );
      methods[parsedResult.type](parsedResult.payload);
      parsed.ResultPayload = JSON.stringify(JSON.stringify("{}"));
      return orig.call(this, args[0], null);
    } catch (e) {
      console.warn(e);
    }
    return orig.call(this, ...args);
  };

  // Thin message layer to communicate with parent
  // Proxy for invoking on DotNet
  window.addEventListener("message", (e) => {
    if (e.data?.intellisage) {
      const { method, args, id } = e.data.intellisage;
      methods[method] = (payload) => {
        e.source.postMessage(
          {
            intellisage: {
              method,
              id,
              payload,
            },
          },
          "*"
        );
      };

      monacoService.invokeMethodAsync(
        "RunAsync",
        method,
        args.map((a) => (typeof a === "object" ? JSON.stringify(a) : a))
      );
    }
  });
}

window.registerService = registerService;
