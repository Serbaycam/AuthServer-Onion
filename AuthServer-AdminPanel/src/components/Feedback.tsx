export function ErrorNotice({ message, retry }: { message: string; retry?: () => void }) {
  if (!message) return null;
  return <div className="notice notice-error" role="alert"><span>{message}</span>
    {retry && <button className="btn btn-outline" onClick={retry}>Tekrar dene</button>}</div>;
}
export function Loading({ message = 'Yükleniyor…' }: { message?: string }) {
  return <div className="loading-state" role="status"><span className="spinner" aria-hidden="true" />{message}</div>;
}
export function SuccessNotice({ message }: { message: string }) {
  return message ? <div className="notice notice-success" role="status">{message}</div> : null;
}
