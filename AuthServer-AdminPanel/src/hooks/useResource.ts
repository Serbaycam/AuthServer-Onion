import { useCallback, useEffect, useState } from 'react';
import { request, errorMessage, isAbort } from '../api';

export function useResource<T>(endpoint: string | null, pollMs = 0) {
  const [result, setResult] = useState<{ endpoint: string | null; data: T | null; error: string; loading: boolean }>(
    { endpoint: null, data: null, error: '', loading: true });
  const [version, setVersion] = useState(0);
  const reload = useCallback(() => setVersion(v => v + 1), []);
  useEffect(() => {
    if (!endpoint) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    const load = async () => {
      try {
        const data = await request<T>(endpoint, { signal: controller.signal });
        if (!controller.signal.aborted) setResult({ endpoint, data, error: '', loading: false });
      } catch (error) {
        if (!controller.signal.aborted && !isAbort(error)) setResult(previous => ({ endpoint,
          data: previous.endpoint === endpoint ? previous.data : null, error: errorMessage(error), loading: false }));
      } finally {
        if (pollMs && !controller.signal.aborted) timer = setTimeout(() => { void load(); }, pollMs);
      }
    };
    void load();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [endpoint, version, pollMs]);
  const current = result.endpoint === endpoint;
  return { data: current ? result.data : null, error: current ? result.error : '', loading: !!endpoint && (!current || result.loading), reload };
}
