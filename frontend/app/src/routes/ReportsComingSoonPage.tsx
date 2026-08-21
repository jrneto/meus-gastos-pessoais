// Placeholder do item "Relatórios" do menu (FEAT-15). A funcionalidade
// de relatórios ainda não existe — esta página só comunica isso, sem
// chamada de API, mantendo o app-shell (sidebar/bottom-nav) visível.
export function ReportsComingSoonPage() {
  return (
    <div className="p-4">
      <h1 className="text-2xl font-semibold">Relatórios</h1>
      <p className="text-muted-foreground">Em breve.</p>
    </div>
  )
}
