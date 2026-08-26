# AI Skills — Справочник навыков ИИ-ассистента

**Версия:** 3.1  
**Дата:** 2026-06-15  
**Проект:** Cultivation World Simulator

---

## Важно

> Этот документ описывает **доступные навыки (Skills)** ИИ-ассистента.  
> Используйте этот справочник для понимания возможностей AI.  
> Скиллы доступны через `Skill(command="Имя")` — вызов загружает инструкции.

---

## Статистика

| Метрика | Значение |
|---------|----------|
| Скиллов с доступом через Skill() tool | 19 |
| AI/Media скиллов (z-ai-web-dev-sdk) | 8 |
| Web/Search скиллов | 3 |
| Document скиллов | 4 |
| Dev/Design скиллов | 2 |
| Utility скиллов | 2 |
| Китайские/специфические скиллы | 25 (в FS, не через Skill()) |

---

## Полный список Skills

### AI & Media Processing (z-ai-web-dev-sdk)

---

#### 1. ASR (Automatic Speech Recognition)

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="ASR")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai asr --file ./audio.wav` |
| **Путь** | `skills/ASR/` |

**Возможности:**
- Транскрибация аудиофайлов в текст
- Распознавание речи (base64 аудио)
- Поддержка различных аудиоформатов
- Возвращает точные текстовые транскрипции

---

#### 2. TTS (Text-to-Speech)

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="TTS")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | Нет |
| **Путь** | `skills/TTS/` |

**Возможности:**
- Синтез речи из текста
- Множество голосов, регулируемая скорость (0.5–2.0)
- Громкость (0–10), формат аудиовыхода
- Ограничение: 1024 символа на запрос (длинный текст — разбивать на части)

---

#### 3. LLM (Large Language Model)

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="LLM")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai chat --prompt "..."` |
| **Путь** | `skills/LLM/` |

**Возможности:**
- Чат-боты и AI-ассистенты
- Многотуровые разговоры с контекстом
- Управление историей разговора и системными промптами
- Генерация текста, анализ, суммаризация
- CLI: поддержка `--stream`, `--thinking`, `--system`, `--output`

---

#### 4. VLM (Vision Language Model)

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="VLM")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai vision --prompt "..." --image "URL"` |
| **Путь** | `skills/VLM/` |

**Возможности:**
- Анализ изображений (URL или base64)
- Понимание визуального контента + текстовый промпт
- Мультимодальные диалоги (текст + изображения)
- OCR, распознавание объектов, описание сцен

---

#### 5. Image-Generation

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="image-generation")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai image-gen --prompt "..."` |
| **Путь** | `skills/image-generation/` |

**Возможности:**
- Генерация изображений из текстовых описаний
- Множество размеров (1024x1024, 512x512, и др.)
- Возврат base64-кодированных изображений
- Поддержка различных моделей генерации

**Для Unity проекта:**
- Иконки предметов и способностей
- Концепт-арт персонажей и монстров
- UI элементы, текстуры, спрайты
- Элементы окружения (деревья, камни, руды)

---

#### 6. Image-Edit

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="image-edit")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai image-edit --prompt "..." --image "..." --output "..."` |
| **Путь** | `skills/image-edit/` |

**Возможности:**
- Редактирование существующих изображений по текстовому описанию
- Создание вариаций и модификаций
- Трансформация визуального контента
- API: `zai.images.generations.edit({ prompt, images, size })`
- CLI: поддержка `-s` для размера (7 размеров: 1024x1024, 768x1344, 864x1152, 1344x768, 1152x864, 1440x720, 720x1440)
- Поддержка URL и base64 изображений

---

#### 7. Image-Search *(НОВЫЙ v3.0)*

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="image-search")` |
| **SDK** | z-ai-web-dev-sdk (CLI only: `z-ai image-search`) |
| **CLI** | `z-ai image-search --query "..." --count 5` |
| **Путь** | `skills/image-search/` |

**Возможности:**
- Поиск изображений в интернете по текстовому запросу
- ZAI собственный сервис поиска (не через HTTP API напрямую)
- Рехостинг на OSS — стабильные URL для встраивания
- Опциональные короткие подписи (captions)
- Локализация через `--gl` (cn, us, jp, kr)
- `--no-rank` — быстрый ответ без подписей
- `--count` 1–20 изображений на запрос

**Для Unity проекта:**
- Поиск референсов для концепт-арта
- Поиск текстур и фотографий окружения
- Встраивание изображений в документы/презентации

---

#### 8. Video-Understanding

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="video-understand")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai vision --prompt "..." --image "video_url"` |
| **Путь** | `skills/video-understand/` |

**Возможности:**
- Анализ видеоконтента (сцены, действия, события)
- Детекция движения и временных последовательностей
- Извлечение информации из кадров
- Многотуровые диалоги о видео
- Оптимизировано для MP4, AVI, MOV
- API: `zai.chat.completions.createVision()` с `video_url`

---

### Web & Search

---

#### 9. Web-Search

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="web-search")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai search --query "..."` |
| **Путь** | `skills/web-search/` |

**Возможности:**
- Веб-поиск в реальном времени
- Структурированные результаты (URL, сниппеты, метаданные)
- Актуальная информация за пределами knowledge cutoff
- Поиск туториалов, решений проблем, документации

---

#### 10. Web-Reader

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="web-reader")` |
| **SDK** | z-ai-web-dev-sdk (backend only) |
| **CLI** | `z-ai web-read --url "..."` |
| **Путь** | `skills/web-reader/` |

**Возможности:**
- Извлечение контента с веб-страниц
- Автоматическое извлечение (заголовок, HTML, время публикации)
- Scraping и парсинг веб-контента
- Чтение Unity документации онлайн

---

#### 11. Agent-Browser

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="agent-browser")` |
| **SDK** | Нет (CLI-утилита) |
| **CLI** | `agent-browser open <url>` |
| **Путь** | `skills/agent-browser/` |

**Возможности:**
- Rust-based headless browser автоматизация
- Клик, ввод, навигация, снапшоты
- Скриншоты страниц
- Node.js fallback для совместимости

---

### Documents

---

#### 12. PDF

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="pdf")` |
| **SDK** | Нет (Python: ReportLab, Playwright, Tectonic) |
| **CLI** | `python3 scripts/pdf.py` |
| **Путь** | `skills/pdf/` |

**Возможности:**
- **Report** — структурированные документы (ReportLab)
- **Creative** — визуальный дизайн (JSON Blueprint → Playwright)
- **Academic** — научные работы (LaTeX/Tectonic)
- **Process** — манипуляция PDF (extract, merge, split, fill forms)
- Автоматическая маршрутизация по типу документа

---

#### 13. DOCX

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="docx")` |
| **SDK** | Нет (python-docx) |
| **CLI** | `python3 scripts/docx.py` |
| **Путь** | `skills/docx/` |

**Возможности:**
- Создание и редактирование Word документов
- Track Changes и комментарии
- Форматирование и стили
- Система дизайна обложек (7 рецептов R1–R7)

---

#### 14. PPTX

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="pptx")` |
| **SDK** | Нет (python-pptx + Beamer + pptxgenjs) |
| **CLI** | `python3 scripts/ppt.py` |
| **Путь** | `skills/pptx/` |

**Возможности:**
- Создание презентаций .pptx
- Редактирование layouts и контента
- **Beamer** — академические/научные презентации (PDF output)
- Заметки докладчика и комментарии
- HTML-PPT pipeline: глобальный CSS + slides_brief.json + sub-agents
- Экспорт через `batch_html2pptx.js`
- Поддержка `image-search` для автоподбора изображений

---

#### 15. XLSX

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="xlsx")` |
| **SDK** | Нет (openpyxl/xlsxwriter) |
| **CLI** | `python3 scripts/xlsx.py` |
| **Путь** | `skills/xlsx/` |

**Возможности:**
- Создание и редактирование Excel (.xlsx, .csv, .tsv)
- Формулы, вычисления, анализ данных
- Графики и диаграммы внутри таблиц
- Конвертация между форматами (CSV/JSON/PDF ↔ XLSX)
- Очистка, объединение, сводка, трансформация данных

**Для Unity проекта:**
- Таблицы баланса техник и предметов
- Экспорт данных конфигураций
- Конфигурации материалов и уровней

---

### Development & Design

---

#### 16. Fullstack-Dev

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="fullstack-dev")` |
| **SDK** | Нет |
| **CLI** | Нет (автоматически доступно в sandbox) |
| **Путь** | `skills/fullstack-dev/` |

**Возможности:**
- Next.js 16 + App Router + TypeScript
- Prisma ORM + SQLite
- shadcn/ui (New York style) + Tailwind CSS 4
- WebSocket/Socket.io для real-time
- Zustand + TanStack Query

---

#### 17. Charts

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="charts")` |
| **SDK** | Нет |
| **CLI** | Нет |
| **Путь** | `skills/charts/` |

**Возможности:**
- **Data charts**: bar, line, pie, scatter, heatmap, radar, candlestick, boxplot, histogram, area, waterfall
- **Structural diagrams**: flowcharts, mind maps, tree, org charts, architecture, ER, class, Gantt, sequence
- **Dashboards**: KPI panels, multi-chart compositions
- Framework routing: matplotlib, seaborn, ECharts, D3.js, Mermaid, Playwright+CSS
- ЗАПРЕЩЕНО: matplotlib/seaborn для структурных диаграмм → только Playwright+CSS

---

### Utility

---

#### 18. Skill-Creator

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="skill-creator")` |
| **SDK** | Нет |
| **CLI** | Нет |
| **Путь** | `skills/skill-creator/` |

**Возможности:**
- Создание новых пользовательских навыков
- Оптимизация и улучшение существующих
- Тестирование и бенчмаркинг производительности
- Итеративный цикл: draft → eval → improve

---

#### 19. Task-Review *(НОВЫЙ v3.0)*

| Параметр | Значение |
|----------|----------|
| **Команда** | `Skill(command="task-review")` |
| **SDK** | Нет |
| **CLI** | Нет |
| **Путь** | `skills/task-review/` |

**Возможности:**
- Автоматическое сохранение пути выполнения сложных задач как повторно используемых навыков
- Триггер: задача с 5+ вызовами инструментов, 3+ шагами, или с ошибками и найденным решением
- Создание/обновление `skills/SKILL-{name}/SKILL.md`
- Формат: name, description, пошаговые инструкции,踩坑记录 (pitfalls)

---

### Китайские/Специфические Skills *(не через Skill() tool)*

Эти скиллы присутствуют в файловой системе, но НЕ доступны через `Skill()` tool. Они работают как автономные инструкции/скрипты.

| # | Имя | Описание | Путь |
|---|-----|----------|------|
| 1 | AI-News-Collectors | AI-новости, агрегация и сортировка по热度 | `skills/ai-news-collectors/` |
| 2 | AMiner-Academic-Search | Академический поиск (27 API AMiner) | `skills/aminer-academic-search/` |
| 3 | AMiner-Daily-Paper | Рекомендация научных статей | `skills/aminer-daily-paper/` |
| 4 | AMiner-Free-Academic | Бесплатный академический поиск (7 API) | `skills/aminer-free-academic/` |
| 5 | Anti-PUA | Анализ манипуляций и токсичных отношений | `skills/anti-pua/` |
| 6 | Auto-Target-Tracker | Автоотслеживание прогресса целей (VLM) | `skills/auto-target-tracker/` |
| 7 | Blog-Writer | Написание блог-постов в авторском стиле | `skills/blog-writer/` |
| 8 | Coding-Agent | Workflow: planning → execution → verification | `skills/coding-agent/` |
| 9 | Content-Strategy | Контент-маркетинг для solopreneur | `skills/content-strategy/` |
| 10 | Content-Analysis | Извлечение мудрости из видео/подкастов | `skills/contentanalysis/` |
| 11 | Dream-Interpreter | AI-толкователь снов (3 перспективы) | `skills/dream-interpreter/` |
| 12 | Get-Fortune-Analysis | 流年运势 отчёты (八字, 十神) | `skills/get-fortune-analysis/` |
| 13 | Gift-Evaluator | Оценка подарков (VLM + search) | `skills/gift-evaluator/` |
| 14 | Interview-Designer | Дизайн интервью (Topgrading методология) | `skills/interview-designer/` |
| 15 | Market-Research-Reports | Рыночные отчёты 50+ страниц (LaTeX) | `skills/market-research-reports/` |
| 16 | Marketing-Mode | 23 маркетинговых навыка в одном | `skills/marketing-mode/` |
| 17 | Mindfulness-Meditation | Управляемые медитации и трекинг | `skills/mindfulness-meditation/` |
| 18 | Multi-Search-Engine | 8 китайских поисковиков без API ключей | `skills/multi-search-engine/` |
| 19 | Podcast-Generate | Генерация подкастов (LLM + TTS) | `skills/podcast-generate/` |
| 20 | Qingyan-Research | Глубокие веб-исследования + HTML отчёты | `skills/qingyan-research/` |
| 21 | SEO-Content-Writer | SEO-оптимизированный контент | `skills/seo-content-writer/` |
| 22 | Skill-Finder-CN | Поиск скиллов на ClawHub | `skills/skill-finder-cn/` |
| 23 | Stock-Analysis | Анализ акций (A股/Гонконг/США) | `skills/stock-analysis-skill/` |
| 24 | Storyboard-Manager | Управление сюжетами и персонажами | `skills/storyboard-manager/` |
| 25 | Writing-Plans | Планирование имплементации (TDD, DRY) | `skills/writing-plans/` |

---

## Изменения с версии 2.0

### Удалено (устаревшие)
| Скилл | Причина удаления |
|-------|-----------------|
| **image-understand** | Удалён из доступных через Skill() — функционал покрывается VLM |
| **video-generation** | Удалён из доступных через Skill() |
| **web-shader-extractor** | Удалён из доступных через Skill() |
| **finance** | Удалён из доступных через Skill() |
| **ui-ux-pro-max** | Удалён из доступных через Skill() |
| **visual-design-foundations** | Удалён из доступных через Skill() |
| **skill-vetter** | Удалён из доступных через Skill() |

### Добавлено (NEW)
| Скилл | Описание |
|-------|----------|
| **image-search** | ZAI собственный сервис поиска изображений — `z-ai image-search` CLI |
| **task-review** | Автосохранение путей выполнения задач как повторно используемых навыков |

### Изменено
| Скилл | Изменение |
|-------|-----------|
| **PPT → PPTX** | Команда переименована: `Skill(command="ppt")` → `Skill(command="pptx")` |
| **image-edit** | Добавлен CLI: `z-ai image-edit -p "..." -i "..." -o "..."`, 7 размеров |
| **video-understand** | CLI через `z-ai vision`, API через `createVision()` с `video_url` |
| **PPTX** | Добавлен HTML-PPT pipeline (global.css + slides_brief.json + sub-agents) |

---

## Рекомендации для Unity проекта

### Поиск и документация Unity

| Skill | Use case | Приоритет |
|-------|----------|-----------|
| **Web-Search** | Поиск Unity 6.3/URP документации, туториалов, решений | Высокий |
| **Web-Reader** | Чтение docs.unity3d.com, блогов, ответов на форумах | Высокий |
| **Agent-Browser** | Интерактивное взаимодействие с веб-страницами Unity | Средний |

### Генерация контента

| Skill | Use case | Приоритет |
|-------|----------|-----------|
| **Image-Generation** | Иконки предметов, концепт-арт, UI элементы, спрайты | Высокий |
| **Image-Search** | Поиск референсов, текстур окружения, фотографий | Высокий |
| **Image-Edit** | Модификация существующих ассетов, вариации | Средний |
| **VLM** | Анализ скриншотов рендеринга, проверка UI | Высокий |
| **TTS** | Озвучка NPC, голосовые подсказки | Низкий |

### Документация и баланс

| Skill | Use case | Приоритет |
|-------|----------|-----------|
| **XLSX** | Таблицы баланса техник, предметов, уровней | Высокий |
| **DOCX** | Дизайн-документы, GDD | Средний |
| **PDF** | Формальные отчёты, white papers | Средний |
| **Charts** | Визуализация данных баланса, архитектурные диаграммы | Средний |

### Разработка

| Skill | Use case | Приоритет |
|-------|----------|-----------|
| **LLM** | Генерация диалогов NPC, описание лора | Средний |
| **Fullstack-Dev** | Web-лаунчер, сайт игры | Низкий |
| **Task-Review** | Сохранение успешных паттернов разработки | Низкий |

---

## Примеры использования для Unity

### Поиск решения проблемы рендеринга

```
1. Web-Search: "Unity 6.3 URP 2D Light2D black sprites"
2. Web-Reader: https://docs.unity3d.com/6000.3/...
3. Синтез решения → применение к RuntimeSceneBuilder.cs
```

### Генерация иконки предмета

```
1. Image-Generation: "Fantasy cultivation herb icon, glowing green, Chinese style, transparent background, 64x64"
2. Сохранение результата в UnityProject/Assets/Sprites/Items/
3. Интеграция через SpriteHelper
```

### Поиск референса для окружения

```
1. Image-Search: "Chinese ancient mountain temple misty forest landscape"
2. Получение стабильных OSS URL для встраивания
3. Использование как референс для Image-Generation
```

### Анализ скриншота проблемы

```
1. VLM: [пользователь загружает скриншот] → "Что не так с рендерингом?"
2. Анализ: спрайты чёрные → нет Light2D → Sprite-Lit-Default без света
3. Применение фикса в RuntimeSceneBuilder
```

### Создание таблицы баланса

```
1. XLSX: Создать таблицу техник культивации
2. Колонки: Name, QiCost, Damage, Cooldown, Element, Level
3. Формулы: DPS = Damage / Cooldown
4. Экспорт данных в JSON для Unity
```

### Архитектурная диаграмма модулей

```
1. Charts: Создать архитектурную диаграмму Hub-and-Spoke
2. 15 модулей → Core → MessagePipe связи
3. Вывод: Playwright+CSS → PNG/SVG
```

---

## Быстрая шпаргалка

### По категориям

| Категория | Skills |
|-----------|--------|
| **AI/Media (SDK)** | ASR, TTS, LLM, VLM, Image-Generation, Image-Edit, Image-Search, Video-Understanding |
| **Web** | Web-Search, Web-Reader, Agent-Browser |
| **Documents** | PDF, DOCX, PPTX, XLSX |
| **Dev/Design** | Fullstack-Dev, Charts |
| **Utility** | Skill-Creator, Task-Review |
| **Китайские/Спец** | 25 скиллов (см. таблицу выше) |

### По приоритету для Unity

| Приоритет | Skills |
|-----------|--------|
| **Критичные** | Web-Search, Web-Reader, Image-Generation, Image-Search, VLM, XLSX |
| **Полезные** | Image-Edit, Charts, DOCX, PDF, LLM, Agent-Browser |
| **Опциональные** | TTS, Video-Understanding, PPTX, Fullstack-Dev, Task-Review |

### По доступу через Skill() tool

| Доступны через Skill() | ASR, LLM, TTS, VLM, image-generation, image-edit, image-search, video-understand, web-search, web-reader, agent-browser, charts, docx, pdf, pptx, xlsx, fullstack-dev, skill-creator, task-review |

---

*Документ создан: 2026-03-30*  
*Редактировано: 2026-06-15 — v3.1: повторная верификация всех 19 Skill() скилов, подтверждена актуальность, исправлена дата*
