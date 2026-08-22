import { CATEGORY_ICONS } from '@/lib/categories/categoryIcons'

interface IconPickerProps {
  value: string | undefined
  onChange: (value: string) => void
  error?: boolean
}

export function IconPicker({ value, onChange, error }: IconPickerProps) {
  return (
    <div
      role="group"
      aria-label="Ícone"
      className={`icon-tile-grid${error ? ' icon-tile-grid--error' : ''}`}
    >
      {CATEGORY_ICONS.map((icon) => (
        <button
          key={icon.value}
          type="button"
          className="icon-tile"
          aria-pressed={value === icon.value}
          aria-label={icon.label}
          title={icon.label}
          onClick={() => onChange(icon.value)}
        >
          <icon.Icon size={16} />
        </button>
      ))}
    </div>
  )
}
