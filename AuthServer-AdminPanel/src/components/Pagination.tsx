export function Pagination({ page, total, onChange, pageSize = 20 }: { page: number; total: number; onChange: (page: number) => void; pageSize?: number }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return <div className="pagination"><span>{total} kayıt · Sayfa {page} / {pages}</span><div className="actions">
    <button className="btn btn-outline" disabled={page <= 1} onClick={() => onChange(page - 1)}>Önceki</button>
    <button className="btn btn-outline" disabled={page >= pages} onClick={() => onChange(page + 1)}>Sonraki</button>
  </div></div>;
}
