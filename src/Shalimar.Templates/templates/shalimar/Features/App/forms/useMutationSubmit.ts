import { useCallback, useState } from 'react'
import type { ZodTypeAny } from 'zod'
import type { ValidationErrors } from '@generated/store'

type MutationOk<T> = { ok: true; value: T }
type MutationErr = { ok: false; error: string } | { ok: false; validation: ValidationErrors }
type MutationResult<T> = MutationOk<T> | MutationErr

export type MutationSpec<TReq, TRes> = {
    defaults: TReq
    schema: ZodTypeAny
    mutate: (req: TReq) => Promise<MutationResult<TRes>>
    serverKey: (path: Array<string | number>) => string
    mapClientIssues?: (issues: Array<{ path: Array<string | number>; message: string }>) => ValidationErrors
}

export function useMutationSubmit<TReq, TRes>(spec: MutationSpec<TReq, TRes>) {
    const [busy, setBusy] = useState(false)
    const [validation, setValidation] = useState<ValidationErrors | null>(null)
    const [error, setError] = useState<string | null>(null)

    const submit = useCallback(
        async (req: TReq): Promise<TRes | null> => {
            if (busy) return null
            setBusy(true)
            setValidation(null)
            setError(null)

            try {
                // Server is source-of-truth: do NOT block submission on client-side validation.
                // The generated schema is provided for optional UX (previews, hints, max-length), but the mutation runs regardless.

                const result = await spec.mutate(req)
                if (result.ok) return result.value

                if ('validation' in result) {
                    setValidation(result.validation)
                    return null
                }

                setError(result.error)
                return null
            } finally {
                setBusy(false)
            }
        },
        [busy, spec],
    )

    return { busy, validation, error, submit }
}

