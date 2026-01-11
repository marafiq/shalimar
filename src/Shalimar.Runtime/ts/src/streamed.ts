import { Store } from '@tanstack/store'
import { useSyncExternalStore } from 'react'

type StreamedStatus = 'idle' | 'streaming' | 'done' | 'error' | 'aborted'

type StreamedEntry = {
    status: StreamedStatus
    items: unknown[]
    lastItem: unknown | undefined
    error: unknown | undefined
    controller: AbortController | null
}

const IDLE: StreamedEntry = { status: 'idle', items: [], lastItem: undefined, error: undefined, controller: null }
const streamedStore = new Store<Record<string, StreamedEntry>>({})

function subscribe(onStoreChange: () => void) {
    return streamedStore.subscribe(() => onStoreChange())
}

function getEntry(href: string): StreamedEntry {
    // Must be referentially stable when unchanged (React useSyncExternalStore contract).
    return streamedStore.state[href] ?? IDLE
}

async function* readNdjsonLines(res: Response): AsyncGenerator<unknown> {
    if (!res.body) throw new Error('Streamed response has no body')
    const reader = res.body.getReader()
    const decoder = new TextDecoder()
    let buf = ''
    while (true) {
        const { done, value } = await reader.read()
        if (done) break
        buf += decoder.decode(value, { stream: true })
        while (true) {
            const idx = buf.indexOf('\n')
            if (idx < 0) break
            const line = buf.slice(0, idx).trim()
            buf = buf.slice(idx + 1)
            if (!line) continue
            yield JSON.parse(line)
        }
    }
    const tail = buf.trim()
    if (tail) yield JSON.parse(tail)
}

/**
 * Manage a finite streamed response (large data with an end).
 * This is "Streamed" mode: the server source is an IAsyncEnumerable written as NDJSON.
 *
 * - Does not start by default.
 * - `start()` reads NDJSON lines and appends up to `maxItems`.
 */
export function useStream<TItem>(ref: { href: string }, opts?: { maxItems?: number }) {
    const href = ref.href
    const maxItems = opts?.maxItems ?? 200

    const entry = useSyncExternalStore(
        subscribe,
        () => getEntry(href),
        () => getEntry(href),
    )

    const start = async () => {
        const current = getEntry(href)
        if (current.status === 'streaming') return

        const controller = new AbortController()
        streamedStore.setState((s) => ({ ...s, [href]: { ...current, status: 'streaming', error: undefined, controller } }))

        try {
            const res = await fetch(href, { headers: { accept: 'application/x-ndjson' }, signal: controller.signal })
            if (!res.ok) throw new Error(`Streamed fetch failed: ${res.status} ${res.statusText}`)

            for await (const item of readNdjsonLines(res)) {
                streamedStore.setState((s) => {
                    const now = s[href] ?? current
                    if (now.controller !== controller) return s
                    const nextItems = [...now.items, item].slice(-maxItems)
                    return { ...s, [href]: { ...now, items: nextItems, lastItem: item } }
                })
            }

            streamedStore.setState((s) => {
                const now = s[href] ?? current
                if (now.controller !== controller) return s
                return { ...s, [href]: { ...now, status: 'done', controller: null } }
            })
        } catch (error) {
            if (controller.signal.aborted) {
                streamedStore.setState((s) => {
                    const now = s[href] ?? current
                    if (now.controller !== controller) return s
                    return { ...s, [href]: { ...now, status: 'aborted', controller: null } }
                })
                return
            }
            streamedStore.setState((s) => {
                const now = s[href] ?? current
                if (now.controller !== controller) return s
                return { ...s, [href]: { ...now, status: 'error', error, controller: null } }
            })
        }
    }

    const abort = () => {
        const current = getEntry(href)
        current.controller?.abort()
    }

    const reset = () => {
        const current = getEntry(href)
        current.controller?.abort()
        streamedStore.setState((s) => ({ ...s, [href]: IDLE }))
    }

    return {
        status: entry.status,
        items: entry.items as TItem[],
        lastItem: entry.lastItem as TItem | undefined,
        error: entry.error,
        start,
        abort,
        reset,
    } as const
}

export function clearStreamedCache() {
    for (const entry of Object.values(streamedStore.state)) {
        entry.controller?.abort()
    }
    streamedStore.setState({})
}

export function invalidateStreamed(href: string) {
    const entry = streamedStore.state[href]
    entry?.controller?.abort()

    streamedStore.setState((s) => {
        if (!(href in s)) return s
        const next = { ...s }
        delete next[href]
        return next
    })
}

