interface CategoryLetterTileProps {
  name: string
}

// Tile decorativo (inicial do nome, sem cor/ícone real) — fiel ao
// design de referência, que usa {{ c.letter }} como avatar puramente
// visual. Compartilhado entre `CategoryList` e `ExpenseDetailDialog`,
// ambas telas já 100% Modernist (sem risco de vazar para telas ainda
// em shadcn/ui, diferente de `CategoryBadge`).
export function CategoryLetterTile({ name }: CategoryLetterTileProps) {
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
        border: '1px solid var(--color-divider)',
        fontSize: '12px',
        fontWeight: 800,
        fontFamily: 'var(--font-heading)',
      }}
    >
      {name.charAt(0).toUpperCase()}
    </span>
  )
}
