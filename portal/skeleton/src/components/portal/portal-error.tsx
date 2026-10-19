'use client';

/**
 * Состояние ошибки: Alert с перечнем errors[] из API (или сетевой ошибки)
 * и кнопкой «Повторить».
 */

import { AlertTriangle, RotateCcw } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';

interface PortalErrorProps {
  errors: string[];
  onRetry: () => void;
}

export function PortalError({ errors, onRetry }: PortalErrorProps) {
  return (
    <Alert
      role="alert"
      className="mt-8 border-rose-500/40 bg-rose-500/[0.07] text-rose-100"
    >
      <AlertTriangle aria-hidden="true" />
      <AlertTitle className="text-rose-200">
        Не удалось загрузить данные портала
      </AlertTitle>
      <AlertDescription className="text-rose-200/70">
        <p>
          Источники читаются из git-репозитория <code className="font-mono">aigame4</code>.
          Проверь, что репозиторий склонирован, и повтори попытку.
        </p>
        {errors.length ? (
          <ul className="mt-2 list-disc pl-4 font-mono text-[11px] leading-relaxed">
            {errors.map((e, i) => (
              <li key={i}>{e}</li>
            ))}
          </ul>
        ) : null}
        <Button
          type="button"
          onClick={onRetry}
          variant="outline"
          className="mt-4 min-h-11 border-amber-500/50 bg-amber-500/10 text-amber-200 hover:bg-amber-500/20 hover:text-amber-100"
        >
          <RotateCcw aria-hidden="true" />
          Повторить
        </Button>
      </AlertDescription>
    </Alert>
  );
}
