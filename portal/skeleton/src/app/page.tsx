/**
 * Единственная страница портала: «Карта проекта Ai-game4».
 * Серверный компонент с метаданными; вся интерактивность —
 * в клиентском каркасе <PortalApp /> (данные тянутся из /api/portal).
 */

import type { Metadata } from 'next';

import { PortalApp } from '@/components/portal/portal-app';

export const metadata: Metadata = {
  title: 'Карта проекта Ai-game4 — портал',
  description:
    'Визуализация карты проекта Ai-game4: дорожки-треки, вехи M0–M7, реестр находок аудита и снимок состояния. Данные читаются из git-репозитория в рантайме.',
};

export default function Home() {
  return <PortalApp />;
}
