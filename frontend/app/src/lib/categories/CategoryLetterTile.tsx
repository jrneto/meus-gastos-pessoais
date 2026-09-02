interface CategoryLetterTileProps {
  name: string
  tipo?: 'despesa' | 'receita'
}

const TIPO_STYLE: Record<'despesa' | 'receita', { border: string; background: string; color: string }> = {
  despesa: {
    border: 'var(--color-accent)',
    background: 'var(--color-accent-100)',
    color: 'var(--color-accent-700)',
  },
  receita: {
    border: 'var(--color-positive)',
    background: 'var(--color-positive-100)',
    color: 'var(--color-positive-700)',
  },
}

// Tile decorativo (inicial do nome, sem cor/ícone real) — fiel ao
// design de referência, que usa {{ c.letter }} como avatar puramente
// visual. Compartilhado entre `CategoryList` e `TransactionDetailDialog`.
// A prop `tipo` (FEAT-22) colore o tile conforme despesa/receita, como
// no `.dc.html`; sem ela, mantém o estilo neutro original (usado hoje
// por `TransactionDetailDialog`, fora do escopo da FEAT-22).
export function CategoryLetterTile({ name, tipo }: CategoryLetterTileProps) {
  const style = tipo ? TIPO_STYLE[tipo] : null

  return (
    <span
      aria-hidden="true"
      style={{
        width: '24px',
        height: '24px',
        flex: 'none',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        border: `1px solid ${style?.border ?? 'var(--color-divider)'}`,
        background: style?.background,
        color: style?.color,
        fontSize: '12px',
        fontWeight: 800,
        fontFamily: 'var(--font-heading)',
      }}
    >
      {name.charAt(0).toUpperCase()}
    </span>
  )
}
