import { useStore } from '@tanstack/react-store'
import { Button, Text } from '@react-spectrum/s2'
import { dismissToast, uiStore } from './store'
import { IconX } from './icons'

function toneClasses(tone: 'neutral' | 'success' | 'warning' | 'danger') {
    switch (tone) {
        case 'success':
            return 'border-emerald-500/30 bg-emerald-500/10'
        case 'warning':
            return 'border-amber-500/30 bg-amber-500/10'
        case 'danger':
            return 'border-red-500/30 bg-red-500/10'
        default:
            return 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-950/40'
    }
}

export function ToastHost() {
    const { toasts } = useStore(uiStore)
    if (toasts.length === 0) return null

    return (
        <div className="fixed right-4 top-4 z-50 flex w-[min(420px,calc(100vw-2rem))] flex-col gap-2" aria-live="polite">
            {toasts.map((t) => (
                <div
                    key={t.id}
                    className={`rounded-xl border p-3 shadow-sm backdrop-blur ${toneClasses(t.tone)}`}
                    data-testid="toast"
                >
                    <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                            <div className="truncate text-sm font-semibold">{t.title}</div>
                            {t.message ? (
                                <Text>
                                    <span className="text-sm opacity-80">{t.message}</span>
                                </Text>
                            ) : null}
                        </div>
                        <Button isQuiet onPress={() => dismissToast(t.id)} aria-label="Dismiss toast">
                            <IconX className="h-4 w-4" />
                        </Button>
                    </div>
                </div>
            ))}
        </div>
    )
}

