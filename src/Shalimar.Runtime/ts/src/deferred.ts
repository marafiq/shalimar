import { Store } from '@tanstack/store'
import { useSyncExternalStore } from 'react'

type DeferredEntry =
    | { status: 'pending'; promise: Promise<void> }
    | { status: 'resolved'; value: unknown }
    | { status: 'rejected'; error: unknown }

const deferredStore = new Store<Record<string, DeferredEntry>>({})

function subscribe(onStoreChange: () => void) {
    return deferredStore.subscribe(() => onStoreChange())
}

function getEntry(href: string) {
    return deferredStore.state[href]
}

async function fetchJson(href: string) {
    const res = await fetch(href, { headers: { accept: 'application/json' } })
    if (!res.ok) throw new Error(`Deferred fetch failed: ${res.status} ${res.statusText}`)
    return (await res.json()) as unknown
}

/**
 * Resolve a deferred handle using React Suspense.
 *
 * The handle is server-owned and must include an `href` to fetch JSON from.
 * The result is cached in a TanStack Store keyed by `href`.
 */
export function useDeferred<T>(ref: { href: string }): T {
    const href = ref.href

    const entry = useSyncExternalStore(
        subscribe,
        () => getEntry(href),
        () => getEntry(href),
    )

    if (!entry) {
        const promise = fetchJson(href)
            .then((value) => {
                deferredStore.setState((s) => {
                    const now = s[href]
                    if (!now || now.status !== 'pending' || now.promise !== promise) return s
                    return { ...s, [href]: { status: 'resolved', value } }
                })
            })
            .catch((error) => {
                deferredStore.setState((s) => {
                    const now = s[href]
                    if (!now || now.status !== 'pending' || now.promise !== promise) return s
                    return { ...s, [href]: { status: 'rejected', error } }
                })
            })

        deferredStore.setState((s) => ({ ...s, [href]: { status: 'pending', promise } }))
        throw promise
    }

    if (entry.status === 'pending') throw entry.promise
    if (entry.status === 'rejected') throw entry.error
    return entry.value as T
}

/**
 * Start fetching a deferred handle without suspending.
 * Useful for prefetching when you anticipate a panel will be opened.
 */
export function prefetchDeferred(ref: { href: string }) {
    const href = ref.href
    const existing = deferredStore.state[href]
    if (existing && existing.status !== 'rejected') return

    const promise = fetchJson(href)
        .then((value) => {
            deferredStore.setState((s) => {
                const now = s[href]
                if (!now || now.status != 'pending' || now.promise !== promise) return s
                return { ...s, [href]: { status: 'resolved', value } }
            })
        })
        .catch((error) => {
            deferredStore.setState((s) => {
                const now = s[href]
                if (!now || now.status != 'pending' || now.promise !== promise) return s
                return { ...s, [href]: { status: 'rejected', error } }
            })
        })

    deferredStore.setState((s) => ({ ...s, [href]: { status: 'pending', promise } }))
}

export function clearDeferredCache() {
    deferredStore.setState({})
}

export function invalidateDeferred(href: string) {
    deferredStore.setState((s) => {
        if (!(href in s)) return s
        const next = { ...s }
        delete next[href]
        return next
    })
}

export function invalidateDeferredByPrefix(prefix: string) {
    deferredStore.setState((s) => {
        let changed = false
        const next: Record<string, DeferredEntry> = {}
        for (const [k, v] of Object.entries(s)) {
            if (k === prefix || k.startsWith(prefix + '?')) {
                changed = true
                continue
            }
            next[k] = v
        }
        return changed ? next : s
    })
}

