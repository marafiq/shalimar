import { Heading, Text } from '@react-spectrum/s2'
import { useWelcomeProps } from '@generated/store'

export default function WelcomePage() {
    const props = useWelcomeProps()

    return (
        <div className="mx-auto flex min-h-full max-w-3xl items-center px-6 py-10">
            <div className="space-y-3">
                <Heading level={1}>{props.title}</Heading>
                <Text UNSAFE_className="text-sm text-zinc-600">
                    Server-first app. This route is wired by Shalimar-generated TanStack virtual routes (no handwritten route modules).
                </Text>
            </div>
        </div>
    )
}

