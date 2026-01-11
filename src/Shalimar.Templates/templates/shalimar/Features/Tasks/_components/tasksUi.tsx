import type React from 'react'

export const selectClass =
    'w-full rounded-md border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-950 outline-none focus:ring-2 focus:ring-blue-500/30 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-50'

export function Field(props: { label: string; children: React.ReactNode }) {
    return (
        <div>
            <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">{props.label}</div>
            {props.children}
        </div>
    )
}

