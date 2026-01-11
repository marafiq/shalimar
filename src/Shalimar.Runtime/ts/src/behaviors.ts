import { useEffect } from 'react'
import { prefetchDeferred } from './deferred'
import { prefetchLazy } from './lazy'

export type ShalimarBehaviors = {
    deferredHrefs: string[]
    lazyHrefs: string[]
    streamedHrefs: string[]
    sseHrefs: string[]
}

export function prefetchBehaviors(behaviors: ShalimarBehaviors | null | undefined) {
    if (!behaviors) return
    for (const href of behaviors.deferredHrefs ?? []) {
        prefetchDeferred({ href })
    }
    for (const href of behaviors.lazyHrefs ?? []) {
        prefetchLazy({ href })
    }
}

/**
 * Execute a server-provided behavior plan after hydration.
 * This is an opt-in hook (apps keep control).
 */
export function useBehaviors(behaviors: ShalimarBehaviors | null | undefined) {
    useEffect(() => {
        prefetchBehaviors(behaviors)
    }, [behaviors])
}

