import { Store } from '@tanstack/store'
import { useSyncExternalStore } from 'react'

type LazyEntry =
    | { status: 'idle' }
    | { status: 'pending'; promise: Promise<unknown> }
    | { status: 'resolved'; value: unknown }
    | { status: 'rejected'; error: unknown }

const IDLE: LazyEntry = { status: 'idle' }

const lazyStore = new Store<Record<string, LazyEntry>>({})

function subscribe(onStoreChange: () => void) {
    return lazyStore.subscribe(() => onStoreChange())
}

function getEntry(href: string): LazyEntry {
    // Must be referentially stable when unchanged (React useSyncExternalStore contract).
    return lazyStore.state[href] ?? IDLE
}

async function fetchJson(href: string) {
    const res = await fetch(href, { headers: { accept: 'application/json' } })
    if (!res.ok) throw new Error(`Lazy fetch failed: ${res.status} ${res.statusText}`)
    return (await res.json()) as unknown
}

/**
 * Manage a lazy handle that only fetches when `load()` is called.
 *
 * This intentionally does NOT suspend by default. (A Suspense wrapper can be added later.)
 */
export function useLazy<T>(ref: { href: string }) {
    const href = ref.href
    const entry = useSyncExternalStore(
        subscribe,
        () => getEntry(href),
        () => getEntry(href),
    )

    const load = () => {
        const current = getEntry(href)
        if (current.status === 'pending') return current.promise as Promise<T>
        if (current.status === 'resolved') return Promise.resolve(current.value as T)

        const promise = fetchJson(href)
            .then((value) => {
                lazyStore.setState((s) => ({ ...s, [href]: { status: 'resolved', value } }))
                return value
            })
            .catch((error) => {
                lazyStore.setState((s) => ({ ...s, [href]: { status: 'rejected', error } }))
                throw error
            })

        lazyStore.setState((s) => ({ ...s, [href]: { status: 'pending', promise } }))
        return promise as Promise<T>
    }

    return {
        status: entry.status,
        data: entry.status === 'resolved' ? (entry.value as T) : undefined,
        error: entry.status === 'rejected' ? entry.error : undefined,
        load,
        reset: () => lazyStore.setState((s) => ({ ...s, [href]: IDLE })),
    } as const
}

export function clearLazyCache() {
    lazyStore.setState({})
}

