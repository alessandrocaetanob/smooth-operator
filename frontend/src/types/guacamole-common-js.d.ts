// Minimal ambient declarations for the small surface of guacamole-common-js
// we use. The library exports only a single default `Guacamole` namespace.
declare module 'guacamole-common-js' {
  namespace Guacamole {
    class Tunnel {
      state: number;
      onstatechange: ((state: number) => void) | null;
      onerror: ((status: { code: number; message: string }) => void) | null;
      static readonly State: {
        CONNECTING: number;
        OPEN: number;
        CLOSED: number;
        UNSTABLE: number;
      };
    }

    class WebSocketTunnel extends Tunnel {
      constructor(url: string);
    }

    class HTTPTunnel extends Tunnel {
      constructor(url: string);
    }

    class Status {
      code: number;
      message: string;
    }

    class InputStream {
      onblob: ((data: string) => void) | null;
      onend: (() => void) | null;
    }

    class OutputStream {
      sendBlob(data: string): void;
      sendEnd(): void;
    }

    class StringReader {
      constructor(stream: InputStream);
      ontext: ((text: string) => void) | null;
      onend: (() => void) | null;
    }

    class StringWriter {
      constructor(stream: OutputStream);
      sendText(text: string): void;
      sendEnd(): void;
    }

    class Display {
      getElement(): HTMLDivElement;
      getWidth(): number;
      getHeight(): number;
      getDefaultLayer(): unknown;
      scale(scale: number): void;
      getScale(): number;
      onresize: ((width: number, height: number) => void) | null;
    }

    class Mouse {
      constructor(element: HTMLElement);
      onmousedown: ((state: unknown) => void) | null;
      onmouseup: ((state: unknown) => void) | null;
      onmousemove: ((state: unknown) => void) | null;
    }

    namespace Mouse {
      class Touchscreen {
        constructor(element: HTMLElement);
        onmousedown: ((state: unknown) => void) | null;
        onmouseup: ((state: unknown) => void) | null;
        onmousemove: ((state: unknown) => void) | null;
      }
    }

    class Keyboard {
      constructor(element: HTMLElement | Document);
      onkeydown: ((keysym: number) => boolean | void) | null;
      onkeyup: ((keysym: number) => void) | null;
      press(keysym: number): void;
      release(keysym: number): void;
      reset(): void;
    }

    class Client {
      constructor(tunnel: Tunnel);
      connect(connectionString?: string): void;
      disconnect(): void;
      getDisplay(): Display;
      sendMouseState(state: unknown): void;
      sendKeyEvent(pressed: number, keysym: number): void;
      sendSize(width: number, height: number): void;
      createClipboardStream(mimetype: string): OutputStream;
      onstatechange: ((state: number) => void) | null;
      onerror: ((status: Status) => void) | null;
      onclipboard: ((stream: InputStream, mimetype: string) => void) | null;
      onname: ((name: string) => void) | null;
    }
  }

  export default Guacamole;
}
