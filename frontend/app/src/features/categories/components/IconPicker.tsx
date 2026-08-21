import { CATEGORY_ICONS } from '@/lib/categories/categoryIcons'
import { cn } from '@/lib/utils'

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
      className={cn(
        'grid grid-cols-4 gap-2 rounded-lg p-1',
        error && 'ring-3 ring-destructive/20',
      )}
    >
      {CATEGORY_ICONS.map((icon) => (
        <button
          key={icon.value}
          type="button"
          aria-pressed={value === icon.value}
          aria-label={icon.label}
          title={icon.label}
          onClick={() => onChange(icon.value)}
          className={cn(
            'flex items-center justify-center rounded-lg border border-input p-2 hover:bg-muted',
            value === icon.value && 'border-primary bg-muted text-primary',
          )}
        >
          <icon.Icon className="size-4" />
        </button>
      ))}
    </div>
  )
}
