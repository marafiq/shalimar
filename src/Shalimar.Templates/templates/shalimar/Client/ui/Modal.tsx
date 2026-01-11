import { createPortal } from 'react-dom'
import { Button, Heading } from '@react-spectrum/s2'
import { IconX } from './icons'

export function Modal(props: { open: boolean; title: string; children: React.ReactNode; onClose: () => void }) {
    if (!props.open) return null

    return createPortal(
        <div className="fixed inset-0 z-50" role="dialog" aria-modal="true" aria-label={props.title}>
            <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={props.onClose} />
            <div className="absolute left-1/2 top-1/2 w-[min(720px,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-black/10 bg-white shadow-xl dark:border-white/10 dark:bg-zinc-950">
                <div className="flex items-start justify-between gap-3 border-b border-black/10 px-5 py-4 dark:border-white/10">
                    <Heading level={3}>{props.title}</Heading>
                    <Button isQuiet onPress={props.onClose} aria-label="Close modal">
                        <IconX />
                    </Button>
                </div>
                <div className="px-5 py-4">{props.children}</div>
            </div>
        </div>,
        document.body,
    )
}

