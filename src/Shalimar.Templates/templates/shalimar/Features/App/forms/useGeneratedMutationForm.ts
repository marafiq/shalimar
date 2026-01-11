import { useCallback, useState } from 'react'
import type { ZodTypeAny } from 'zod'
import type { ValidationErrors } from '@generated/store'

type MutationOk<T> = { ok: true; value: T }
type MutationErr = { ok: false; error: string } | { ok: false; validation: ValidationErrors }
type MutationResult<T> = MutationOk<T> | MutationErr

function toPascalCase(s: string) {
    if (!s) return s
    return s.length === 1 ? s.toUpperCase() : s[0].toUpperCase() + s.slice(1)
}

function zodPathToServerKey(path: Array<string | number>): string {
    if (path.length === 0) return 'Request'
    const [first, ...rest] = path
    let key = typeof first === 'string' ? toPascalCase(first) : String(first)
    for (const seg of rest) {
        if (typeof seg === 'number') key += `[${seg}]`
        else key += `.${seg}`
    }
    return key
}

function zodIssuesToValidationErrors(schemaError: { issues: Array<{ path: Array<string | number>; message: string }> }): ValidationErrors {
    const out: ValidationErrors = {}
    for (const i of schemaError.issues) {
        const k = zodPathToServerKey(i.path)
        out[k] ??= []
        out[k].push(i.message)
    }
    return out
}

export function useGeneratedMutationForm<TReq, TRes>(opts: {
    mutate: (req: TReq) => Promise<MutationResult<TRes>>
    schema?: ZodTypeAny
}) {
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
                if (opts.schema) {
                    const parsed = opts.schema.safeParse(req)
                    if (!parsed.success) {
                        setValidation(zodIssuesToValidationErrors(parsed.error))
                        return null
                    }
                }

                const result = await opts.mutate(req)
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
        [busy, opts],
    )

    return {
        busy,
        validation,
        error,
        setValidation,
        submit,
    }
}

