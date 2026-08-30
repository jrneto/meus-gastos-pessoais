export interface CategoryItem {
  id: string
  nome: string
  tipo: 'despesa' | 'receita'
  orcamentoMensalCents: number | null
  createdAt: string
}
