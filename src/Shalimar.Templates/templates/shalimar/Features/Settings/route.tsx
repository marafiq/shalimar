import { createFileRoute } from '@tanstack/react-router'
import { Heading, Text } from '@react-spectrum/s2'

export const Route = createFileRoute('/settings')({
    component: SettingsRoute,
})

function SettingsRoute() {
    return (
        <div className="max-w-2xl">
            <Heading level={1}>Settings</Heading>
            <Text>
                Placeholder settings area. This will eventually be generated from server-defined configuration and
                validations.
            </Text>
        </div>
    )
}

