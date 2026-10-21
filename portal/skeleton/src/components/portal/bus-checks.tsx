'use client';

/**
 * Блок «Проверка полноты шины»: сверка издателей с подписчиками и
 * регистраций с запросами. Отвечает на вопрос владельца:
 * «всё ли публикуется и забирается с центральной шины?»
 *
 * Шесть проверок (данные — из checks bus_map.json, расчёт — gen_bus_map.py):
 *   events_no_publisher    забирается, но не публикуется   — ошибка (красный)
 *   events_no_subscriber   публикуется, но не забирается   — предупреждение
 *   services_unresolved    запрашивается, но нет регистрации — ошибка
 *   services_unconsumed    зарегистрировано, но никто не забирает — предупреждение
 *   contracts_unused       контракт объявлен, но не используется    — заметка
 *   contracts_local_unscoped сообщение вне Core/Messaging/Contracts — заметка
 */

import { CircleAlert, CircleCheck, Inbox, TriangleAlert } from 'lucide-react';

import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import type { BusChecks } from '@/lib/portal/types';

type Tone = 'danger' | 'warn' | 'note';

const TONE: Record<Tone, { border: string; head: string; badge: string; icon: string }> = {
  danger: {
    border: 'border-red-400/30 bg-red-400/[0.04]',
    head: 'text-red-300',
    badge: 'border-red-400/40 text-red-300',
    icon: 'text-red-400',
  },
  warn: {
    border: 'border-amber-500/30 bg-amber-500/[0.04]',
    head: 'text-amber-300',
    badge: 'border-amber-500/40 text-amber-300',
    icon: 'text-amber-400',
  },
  note: {
    border: 'border-stone-700/70 bg-zinc-900/40',
    head: 'text-stone-300',
    badge: 'border-stone-600 text-stone-300',
    icon: 'text-stone-400',
  },
};

function CheckCard({
  title,
  hint,
  tone,
  count,
  children,
}: {
  title: string;
  hint: string;
  tone: Tone;
  count: number;
  children: React.ReactNode;
}) {
  const t = TONE[tone];
  const clean = count === 0;
  return (
    <div className={`rounded-lg border p-3.5 ${clean ? 'border-stone-800/70 bg-zinc-900/40' : t.border}`}>
      <Collapsible>
        <div className="flex items-center gap-2">
          {clean ? (
            <CircleCheck aria-hidden="true" className="size-4 shrink-0 text-emerald-400" />
          ) : (
            <span className={t.icon} aria-hidden="true">
              {tone === 'danger' ? (
                <CircleAlert className="size-4" />
              ) : (
                <TriangleAlert className="size-4" />
              )}
            </span>
          )}
          <p className={`text-xs font-semibold ${clean ? 'text-stone-300' : t.head}`}>{title}</p>
          <span
            className={`ml-auto rounded border px-1.5 py-0.5 font-mono text-[11px] tabular-nums ${
              clean ? 'border-stone-700 text-stone-400' : t.badge
            }`}
          >
            {count}
          </span>
        </div>
        <p className="mt-1 text-[11px] leading-snug text-stone-500">{hint}</p>
        {!clean ? (
          <CollapsibleTrigger className="mt-2 text-[11px] font-medium text-stone-400 underline decoration-stone-700 underline-offset-4 hover:text-stone-200">
            {count} — показать список
          </CollapsibleTrigger>
        ) : null}
        {!clean ? (
          <CollapsibleContent>
            <div className="mt-2.5 flex max-h-72 flex-wrap gap-1.5 overflow-y-auto pr-1">
              {children}
            </div>
          </CollapsibleContent>
        ) : null}
      </Collapsible>
    </div>
  );
}

function Chip({ children, title }: { children: React.ReactNode; title?: string }) {
  return (
    <span
      title={title}
      className="rounded border border-stone-700/80 bg-zinc-950/70 px-1.5 py-0.5 font-mono text-[10px] leading-relaxed text-stone-300"
    >
      {children}
    </span>
  );
}

export function BusChecksSection({ checks }: { checks: BusChecks }) {
  return (
    <section aria-label="Проверка полноты шины" className="flex flex-col gap-3">
      <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.2em] text-stone-500">
        <Inbox aria-hidden="true" className="size-4 text-amber-400" />
        Проверка полноты — всё ли публикуется и забирается
      </p>
      <div className="grid gap-3 md:grid-cols-2">
        <CheckCard
          title="Забирается, но не публикуется"
          hint="Есть подписка, но издателя нет ни в продакшн-, ни в QA-коде: событие никогда не приходит."
          tone="danger"
          count={checks.events_no_publisher.length}
        >
          {checks.events_no_publisher.map((x) => (
            <Chip key={x.event} title={`подписанты: ${x.subscribers.join(', ')}`}>
              {x.event} <span className="text-stone-600">←{x.subscribers.length}</span>
            </Chip>
          ))}
        </CheckCard>

        <CheckCard
          title="Публикуется, но не забирается"
          hint="Продакшн-подписчиков нет (только QA-харнессы или никто). Событие уходит «в пустоту»."
          tone="warn"
          count={checks.events_no_subscriber.length}
        >
          {checks.events_no_subscriber.map((x) => (
            <Chip
              key={x.event}
              title={`издатели: ${x.publishers.join(', ')}${x.debug_only ? '; потребляют только QA-харнессы' : ''}`}
            >
              {x.event}
              <span className="text-stone-600"> →{x.publishers.length}</span>
              {x.debug_only ? <span className="text-amber-500/80"> ·QA</span> : null}
            </Chip>
          ))}
        </CheckCard>

        <CheckCard
          title="Запрашивается, но не предоставляется"
          hint="Требование без регистрации в DI — упало бы при сборке контейнера."
          tone="danger"
          count={checks.services_unresolved.length}
        >
          {checks.services_unresolved.map((x) => (
            <Chip key={x.service} title={`запрашивают: ${x.required_by.join(', ')}`}>
              {x.service} <span className="text-stone-600">←{x.required_by.length}</span>
            </Chip>
          ))}
        </CheckCard>

        <CheckCard
          title="Предоставляется, но не запрашивается"
          hint="Сервис зарегистрирован, но ни один узел его не снимает с шины (мёртвый регистр)."
          tone="warn"
          count={checks.services_unconsumed.length}
        >
          {checks.services_unconsumed.map((x) => (
            <Chip key={x.service} title={`предоставляет: ${x.provided_by}`}>
              {x.service} <span className="text-stone-600">от {x.provided_by}</span>
            </Chip>
          ))}
        </CheckCard>

        <CheckCard
          title="Мёртвые контракты"
          hint="Объявлены в Core/Messaging/Contracts, но не публикуются и не слушаются."
          tone="note"
          count={checks.contracts_unused.length}
        >
          {checks.contracts_unused.map((name) => (
            <Chip key={name}>{name}</Chip>
          ))}
        </CheckCard>

        <CheckCard
          title="Сообщения вне Core-контрактов"
          hint="Структуры-сообщения, живущие вне Core/Messaging/Contracts."
          tone="note"
          count={checks.contracts_local_unscoped.length}
        >
          {checks.contracts_local_unscoped.map((x) => (
            <Chip key={x.event} title={`источник: ${x.origin}`}>
              {x.event}
            </Chip>
          ))}
        </CheckCard>
      </div>
    </section>
  );
}
