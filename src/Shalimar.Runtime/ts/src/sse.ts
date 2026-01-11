import { Store } from '@tanstack/store'
import { useSyncExternalStore } from 'react'

type SseStatus = 'idle' | 'connecting' | 'open' | 'error' | 'closed'

type SseEntry = {
    status: SseStatus
    events: unknown[]
    lastEvent: unknown | undefined
    error: unknown | undefined
    es: EventSource | null
}

const IDLE: SseEntry = { status: 'idle', events: [], lastEvent: undefined, error: undefined, es: null }
const sseStore = new Store<Record<string, SseEntry>>({})

function subscribe(onStoreChange: () => void) {
    return sseStore.subscribe(() => onStoreChange())
}

function getEntry(href: string): SseEntry {
    // Must be referentially stable when unchanged (React useSyncExternalStore contract).
    return sseStore.state[href] ?? IDLE
}

/**
 * Manage an SSE subscription (realtime / long-lived).
 *
 * - Does not connect by default (keeps initial render deterministic).
 * - `start()` opens an EventSource to the server-owned `href`.
 * - The subscription is cached by href so multiple components share the connection.
 */
export function useSse<TEvent>(ref: { href: string }, opts?: { maxEvents?: number }) {
    const href = ref.href
    const maxEvents = opts?.maxEvents ?? 50

    const entry = useSyncExternalStore(
        subscribe,
        () => getEntry(href),
        () => getEntry(href),
    )

    const start = () => {
        const current = getEntry(href)
        if (current.es) return

        const es = new EventSource(href)
        sseStore.setState((s) => ({ ...s, [href]: { ...current, status: 'connecting', error: undefined, es } }))

        es.addEventListener('open', () => {
            sseStore.setState((s) => {
                const now = s[href] ?? current
                if (now.es !== es) return s
                return { ...s, [href]: { ...now, status: 'open', error: undefined } }
            })
        })

        es.addEventListener('message', (e) => {
            const data = (e as MessageEvent).data
            let parsed: unknown = data
            try {
                parsed = JSON.parse(String(data))
            } catch {
                // keep as string
            }

            sseStore.setState((s) => {
                const now = s[href] ?? current
                if (now.es !== es) return s
                const nextEvents = [...now.events, parsed].slice(-maxEvents)
                return { ...s, [href]: { ...now, status: 'open', events: nextEvents, lastEvent: parsed } }
            })
        })

        es.addEventListener('error', () => {
            sseStore.setState((s) => {
                const now = s[href] ?? current
                if (now.es !== es) return s
                return { ...s, [href]: { ...now, status: 'error', error: 'SSE error' } }
            })
        })
    }

    const stop = () => {
        const current = getEntry(href)
        if (!current.es) return
        current.es.close()
        sseStore.setState((s) => ({ ...s, [href]: { ...current, status: 'closed', es: null } }))
    }

    const reset = () => {
        const current = getEntry(href)
        if (current.es) current.es.close()
        sseStore.setState((s) => ({ ...s, [href]: IDLE }))
    }

    return {
        status: entry.status,
        events: entry.events as TEvent[],
        lastEvent: entry.lastEvent as TEvent | undefined,
        error: entry.error,
        start,
        stop,
        reset,
    } as const
}

export function clearSseCache() {
    for (const entry of Object.values(sseStore.state)) {
        if (entry.es) entry.es.close()
    }
    sseStore.setState({})
}

