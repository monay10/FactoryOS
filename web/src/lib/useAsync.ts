import { useEffect, useState } from "react";

export interface AsyncState<T> {
  data: T | null;
  error: string | null;
  loading: boolean;
}

/**
 * Runs an async loader and tracks its state. Re-runs whenever `deps` change. Ignores a resolved
 * result if the component unmounted or the deps changed first (no state updates after teardown).
 */
export function useAsync<T>(loader: () => Promise<T>, deps: unknown[]): AsyncState<T> {
  const [state, setState] = useState<AsyncState<T>>({ data: null, error: null, loading: true });

  useEffect(() => {
    let live = true;
    setState({ data: null, error: null, loading: true });
    loader()
      .then((data) => {
        if (live) setState({ data, error: null, loading: false });
      })
      .catch((err: unknown) => {
        if (live) setState({ data: null, error: err instanceof Error ? err.message : String(err), loading: false });
      });
    return () => {
      live = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return state;
}
