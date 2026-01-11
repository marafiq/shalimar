import { Store } from '@tanstack/store'
import { useSyncExternalStore } from 'react'

type StreamStatus = 'idle' | 'connecting' | 'open' | 'error' | 'closed'

type StreamEntry = {
    status: StreamStatus
    events: unknown[]
    lastEvent: unknown | undefined
    error: unknown | undefined
    // Keep a singleton EventSource per href
    es: EventSource | null
}

const IDLE: StreamEntry = { status: 'idle', events: [], lastEvent: undefined, error: undefined, es: null }

const streamStore = new Store<Record<string, StreamEntry>>({})

function subscribe(onStoreChange: () => void) {
    return streamStore.subscribe(() => onStoreChange())
}

function getEntry(href: string): StreamEntry {
    // Must be referentially stable when unchanged (React useSyncExternalStore contract).
    return streamStore.state[href] ?? IDLE
}

/**
 * Manage an SSE stream. This is "Streamed" mode.
 *
 * - Does not connect by default (to keep SSR/prod snapshots stable).
 * - `start()` opens an EventSource to the server-owned `href`.
 * - The stream is cached by href so multiple components share the connection.
 */
export function useStream<TEvent>(ref: { href: string }, opts?: { maxEvents?: number }) {
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
        streamStore.setState((s) => ({ ...s, [href]: { ...current, status: 'connecting', error: undefined, es } }))

        es.addEventListener('open', () => {
            streamStore.setState((s) => {
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

            streamStore.setState((s) => {
                const now = s[href] ?? current
                if (now.es !== es) return s
                const nextEvents = [...now.events, parsed].slice(-maxEvents)
                return { ...s, [href]: { ...now, status: 'open', events: nextEvents, lastEvent: parsed } }
            })
        })

        es.addEventListener('error', () => {
            streamStore.setState((s) => {
                const now = s[href] ?? current
                if (now.es !== es) return s
                return { ...s, [href]: { ...now, status: 'error', error: 'Stream error' } }
            })
        })
    }

    const stop = () => {
        const current = getEntry(href)
        if (!current.es) return
        current.es.close()
        streamStore.setState((s) => ({ ...s, [href]: { ...current, status: 'closed', es: null } }))
    }

    const reset = () => {
        const current = getEntry(href)
        if (current.es) current.es.close()
        streamStore.setState((s) => ({ ...s, [href]: IDLE }))
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

export function clearStreamCache() {
    for (const entry of Object.values(streamStore.state)) {
        if (entry.es) entry.es.close()
    }
    streamStore.setState({})
}

