/**
 * @shalimar/runtime
 * Client-side runtime for Shalimar framework
 */

export { useShalimarContext, ShalimarProvider } from './context'
export { createRouter } from './router'
export type { ShalimarContext, ShalimarProviderProps } from './types'

export { useDeferred, prefetchDeferred, clearDeferredCache, invalidateDeferred, invalidateDeferredByPrefix } from './deferred'
export { useLazy, prefetchLazy, clearLazyCache, invalidateLazy, invalidateLazyByPrefix } from './lazy'
export { useStream, clearStreamedCache, invalidateStreamed, invalidateStreamedByPrefix } from './streamed'
export { useSse, clearSseCache, invalidateSse, invalidateSseByPrefix } from './sse'

export { prefetchBehaviors, useBehaviors } from './behaviors'

































