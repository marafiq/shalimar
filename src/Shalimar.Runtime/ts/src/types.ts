import type { ReactNode } from 'react'

/**
 * Base context interface injected by server as __SHALIMAR_CONTEXT__
 */
export interface ShalimarContext {
    environment: string
    [key: string]: unknown
}

/**
 * Props for the ShalimarProvider component
 */
export interface ShalimarProviderProps {
    children: ReactNode
    context?: ShalimarContext
}

/**
 * Global window augmentation for Shalimar injected data
 */
declare global {
    interface Window {
        __SHALIMAR_CONTEXT__?: ShalimarContext
        __SHALIMAR_VERSION__?: string
    }
}
