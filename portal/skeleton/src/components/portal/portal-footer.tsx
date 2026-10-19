/**
 * Sticky-футер: корневой wrapper — min-h-screen flex flex-col, футер — mt-auto,
 * iOS safe-area учтена через pb-[env(safe-area-inset-bottom)].
 */

export function PortalFooter() {
  return (
    <footer className="mt-auto border-t border-stone-800/70 bg-zinc-950 pb-[env(safe-area-inset-bottom)]">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-1 px-4 py-4 text-[11px] leading-relaxed text-stone-500 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <p>
          Ai-game4 · Cultivation World Simulator ·{' '}
          <span className="text-stone-600">Данные: git-репозиторий aigame4</span>
        </p>
        <p className="sm:text-right">
          Восстановление:{' '}
          <code className="font-mono text-stone-400">
            bash aigame4/portal/restore_portal.sh
          </code>
        </p>
      </div>
    </footer>
  );
}
