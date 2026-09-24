import { useCallback, useRef } from "react";

type PageCommandHandler = (data: unknown) => unknown;

interface PageCommandOptions {
  readonly after?: (response: never) => void;
}

interface PageCommandCall {
  readonly name: string;
  readonly data: unknown;
}

const handlers = new Map<string, PageCommandHandler>();

/** Every page command a component executed, in order, with the payload it sent. */
export const pageCommandCalls: PageCommandCall[] = [];

/** Sets the server response for a page command. */
export const mockPageCommand = (name: string, handler: PageCommandHandler) => {
  handlers.set(name, handler);
};

export const resetPageCommands = () => {
  handlers.clear();
  pageCommandCalls.length = 0;
};

export const commandData = (name: string) => pageCommandCalls.filter((call) => call.name === name).map((call) => call.data);

// Like the real hook, `execute` keeps a stable identity across renders, so components can list it
// as an effect dependency, and `after` receives the command's response.
export const usePageCommand = (name: string, options?: PageCommandOptions) => {
  const optionsRef = useRef(options);
  optionsRef.current = options;

  const execute = useCallback(
    async (data?: unknown) => {
      pageCommandCalls.push({ name, data });
      const handler = handlers.get(name);
      if (!handler) {
        throw new Error(`No mock response for page command "${name}".`);
      }

      const response = await handler(data);
      optionsRef.current?.after?.(response as never);
      return response;
    },
    [name],
  );

  return { execute };
};

export const useAntiForgery = () => ({
  getXsrfHeader: () => ({ "X-XSRF-TOKEN": "test-token" }),
  refreshToken: async () => undefined,
});
