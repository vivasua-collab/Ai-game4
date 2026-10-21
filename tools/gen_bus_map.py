#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
gen_bus_map.py — генератор карты центральной шины Ai-game4.

Сканирует game/src (Core + 17 Modules + Entry + Adapter) и извлекает
ФАКТИЧЕСКОЕ использование двух каналов центральной шины:

  1. EventBus     — publish/subscribe контрактов (readonly struct из
                    Core/Messaging/Contracts);
  2. DI-контейнер — register (модуль отдаёт) / resolve + [Inject] + ctor
                    (модуль запрашивает).

Результат: portal/data/bus_map.json — данные для вкладки «Схема шины»
веб-портала. Обновление — ТОЛЬКО по требованию:
  правки кода → python3 tools/gen_bus_map.py → коммит.
  python3 tools/gen_bus_map.py --check   # детектор дрейфа (exit 1)
  python3 tools/gen_bus_map.py --stdout  # печать без записи

Извлекаемые паттерны (по фактическому коду):
  publish:    .Publish(new XxxEvent(..)) | .Publish(in X) | .Publish(default X)
  subscribe:  .Subscribe((in X e) => ..) | .Subscribe<X>(..) | .Subscribe(OnX)
              (method-group → тип из сигнатуры `void OnX(in X e)`)
  inject:     [Inject] private readonly T _f = null!; | [Inject] public T P {get;set;}
  register:   .Register<IFace, Impl>() | .Register<Impl>() | .RegisterInstance<T>(x)
              | .RegisterInstance(new Cfg) | .RegisterInstance(ident) — тип из
              `var ident = new Cfg ...` в том же файле
  resolve:    .Resolve<T>() | .ResolveAll<T>() (маркерные контракты ISaveable/…)
  ctor:       public Ctor(IApi a, Impl b) — только классов, зарегистрированных в DI

QA-харнессы (*SimDebug.cs, Audit*.cs) — отдельный узел Adapter.Scene.Debug:
их подписки НЕ считаются продакшн-потреблением при проверках полноты
(иначе «тестовый слушатель» маскирует осиротевшее событие).
"""

import argparse
import json
import re
import subprocess
import sys
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "game" / "src"
OUT = ROOT / "portal" / "data" / "bus_map.json"

SCHEMA_VERSION = 1

# Канонический порядок модулей (DI_AND_EVENTBUS §1.2 / GameLifetimeScope).
MODULE_ORDER = [
    "World", "Tile", "Body", "Qi", "Buff", "Inventory", "Combat", "Formation",
    "NPC", "Player", "Quest", "Interaction", "Trade", "UI", "Charger", "Save",
    "Generator",
]

MODULE_DESCRIPTIONS = {
    "World": "Локации, фракции, время, события мира",
    "Tile": "Тайловая карта, ресурсы",
    "Body": "Части тела, двойная HP, ампутации",
    "Qi": "Ци, ядро, проводимость, прорывы",
    "Buff": "Баффы, мягкие капы, иммунитеты",
    "Inventory": "Инвентарь, экипировка, крафт, лут",
    "Combat": "Бой: 11-слойный пайплайн урона",
    "Formation": "Магические массивы, контур, пул Ци",
    "NPC": "Спавн, AI, группы, отношения, трупы",
    "Player": "Игрок, ввод, техники, таргетинг",
    "Quest": "Квесты, прогресс, награды",
    "Interaction": "Взаимодействия, диалоги",
    "Trade": "Торговля, духовные камни",
    "UI": "HUD, тосты, модалки (engine-agnostic слой)",
    "Charger": "Зарядники Ци: слоты, буфер, тепло",
    "Save": "Сохранения, агрегация ISaveable",
    "Generator": "Генерация предметов/техник/формаций",
}

NODE_META = {
    "Entry": ("Entry", "GameSession, SceneOrchestrator, фазы сборки"),
    "Adapter.UI": ("Adapter", "Godot-окна: инвентарь, персонаж, торговля…"),
    "Adapter.Scene": ("Adapter", "Godot-сцена: GameBoot, рендеры, контроллер"),
    "Adapter.Scene.Debug": ("Adapter", "QA-харнессы (*SimDebug) — вне продакшн-потребления"),
    "Adapter.Input": ("Adapter", "Слой ввода Godot"),
    "Adapter.Persistence": ("Adapter", "Файловый слой сохранений (ISaveFileHandler)"),
}

DEBUG_NODE = "Adapter.Scene.Debug"

# ────────────────────────────── вспомогательные ─────────────────────────────


def strip_comments(text: str) -> str:
    """Удалить // и /* */ комментарии: в migration-комментариях встречаются
    примеры вида `builder.Register<I, X>()` — источники ложных срабатываний."""
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    out_lines = []
    for line in text.split("\n"):
        cut = None
        in_str = False
        escape = False
        i = 0
        while i < len(line):
            ch = line[i]
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_str = not in_str
            elif ch == "/" and not in_str and i + 1 < len(line) and line[i + 1] == "/":
                cut = i
                break
            i += 1
        out_lines.append(line if cut is None else line[:cut])
    return "\n".join(out_lines)


def tail(name: str) -> str:
    """`Core.Messaging.Contracts.ToastShownEvent` → `ToastShownEvent`."""
    return name.rsplit(".", 1)[-1]


def node_for(path: Path):
    rel = path.relative_to(SRC)
    parts = rel.parts
    if parts[0] == "Core":
        return None  # хаб: контракты парсятся отдельно
    if parts[0] == "Modules":
        return parts[1]
    if parts[0] == "Entry":
        return "Entry"
    if parts[0] == "Adapter":
        sub = parts[1]
        stem = path.stem
        if "SimDebug" in stem or stem.startswith("Audit"):
            return DEBUG_NODE
        if sub == "UI":
            return "Adapter.UI"
        if sub == "Scene":
            return "Adapter.Scene"
        if sub == "Di":
            return "Adapter.Scene"  # ContainerAdapter — хелпер сцены
        if sub == "Input":
            return "Adapter.Input"
        if sub == "Persistence":
            return "Adapter.Persistence"
    return None


# ────────────────────────────── регексы ─────────────────────────────────────

RE_PUBLISH = re.compile(r"\.Publish\(\s*(?:new|in|default)\s+([A-Za-z_][\w.]*)")
RE_SUB_LAMBDA = re.compile(r"\.Subscribe\(\s*(?:static\s+)?\(?\s*in\s+([A-Za-z_][\w.]*)")
RE_SUB_GENERIC = re.compile(r"\.Subscribe<\s*([A-Za-z_][\w.]*)\s*>")
RE_SUB_METHOD = re.compile(r"\.Subscribe\(\s*([A-Za-z_]\w*)\s*\)")
RE_HANDLER = re.compile(r"\bvoid\s+([A-Za-z_]\w*)\s*\(\s*in\s+([A-Za-z_][\w.]*)")

RE_INJECT_LINE = re.compile(r"\[Inject\][^\n]*")
RE_INJECT_JOIN = re.compile(r"\[\s*Inject\s*\][^\S\n]*\n[\t ]*")
RE_MODIFIERS = re.compile(
    r"^(?:\s*(?:public|private|protected|internal|static|readonly|required|new|"
    r"partial|volatile|extern|unsafe)\s*)+")
RE_MEMBER_DECL = re.compile(r"^([A-Za-z_][\w.]*(?:<[^>]+>)?)\s+(\w+)\s*(?:=|;|\{)")

RE_REGISTER_PAIR = re.compile(r"\.Register<\s*([A-Za-z_][\w.]*)\s*,\s*([A-Za-z_][\w.]*)\s*>")
RE_REGISTER_SELF = re.compile(r"\.Register<\s*([A-Za-z_][\w.]*)\s*>")
RE_REG_INSTANCE_TYPED = re.compile(r"\.RegisterInstance<\s*([A-Za-z_][\w.]*)\s*>")
RE_REG_INSTANCE_NEW = re.compile(r"\.RegisterInstance\(\s*new\s+([A-Za-z_][\w.]*)")
RE_REG_INSTANCE_ID = re.compile(r"\.RegisterInstance\(\s*([a-z_]\w*)\s*\)")
RE_VAR_NEW = re.compile(
    r"\b(?:var|[A-Za-z_][\w.]*)\s+([a-z_]\w*)\s*=\s*new\s+([A-Za-z_]\w*(?:<[^>]+>)?)")
RE_RESOLVE = re.compile(r"\.Resolve<\s*([A-Za-z_][\w.]*)\s*>")
RE_RESOLVE_ALL = re.compile(r"\.ResolveAll<\s*([A-Za-z_][\w.]*)\s*>")
RE_CTOR = re.compile(r"public\s+([A-Z]\w*)\s*\(([^)]*)\)")
RE_CLASS = re.compile(
    r"(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
    r"(?:class|struct)\s+([A-Za-z_]\w*)\s*(?::\s*([^{]+))?\s*\{"
)
RE_STRUCT_DEF = re.compile(r"(?:public|internal)\s+readonly\s+struct\s+([A-Za-z_]\w*)")
RE_INTERFACE_DEF = re.compile(r"(?:public|internal)\s+(?:partial\s+)?interface\s+([A-Za-z_]\w*)")


def parse_inject_line(line: str):
    """`[Inject] private readonly IPublisher<X> _p = null!;` → тип-токен."""
    body = line.split("[Inject]", 1)[1]
    if "typeof(" in body or "GetCustomAttribute" in body:
        return None
    body = RE_MODIFIERS.sub("", body, count=1)
    m = RE_MEMBER_DECL.match(body)
    if not m:
        return None
    return m.group(1).strip()


def split_inject_type(type_token: str):
    """`IPublisher<X>` → ('pub', X); `ISubscriber<X>` → ('sub', X); иначе ('service', T).
    Учитывает квалифицированные формы: Core.Events.IPublisher<Contracts.X>."""
    m = re.search(r"\b(IPublisher|ISubscriber)<\s*([A-Za-z_][\w.]*)\s*>", type_token)
    if m:
        return ("pub" if m.group(1) == "IPublisher" else "sub"), tail(m.group(2))
    return "service", tail(type_token)


def parse_ctor_params(params: str, known_types: set):
    out = []
    for chunk in params.split(","):
        chunk = chunk.strip()
        if not chunk:
            continue
        chunk = re.sub(r"=\s*[^,=]+$", "", chunk).strip()  # default-значения
        m = re.match(r"([A-Za-z_][\w.<>]*)\s+\w+$", chunk)
        if not m:
            continue
        t = tail(m.group(1))
        if t in known_types:  # только типы проекта (отсекаем int/string/…)
            out.append(t)
    return out


# ────────────────────────── проход 1: вселенная типов ───────────────────────


def scan_universe():
    texts = {}
    for p in SRC.rglob("*.cs"):
        texts[p] = strip_comments(p.read_text(encoding="utf-8", errors="replace"))

    contracts = {}        # имя → файл-группа (Core/Messaging/Contracts)
    local_structs = {}    # readonly struct вне Core-контрактов
    class_bases = {}      # класс → [базы]
    interfaces = set()
    known_types = set()   # все объявленные в проекте имена типов
    registered_impls = set()
    provides_by_node = defaultdict(list)
    service_keys = set()

    for p, text in texts.items():
        node = node_for(p)
        rel = str(p.relative_to(ROOT)).replace("\\", "/")
        is_debug = node == DEBUG_NODE

        for m in RE_STRUCT_DEF.finditer(text):
            name = m.group(1)
            known_types.add(name)
            if "Messaging/Contracts" in rel:
                contracts[name] = p.stem.replace("Contracts", "") or "Game"
            else:
                local_structs[name] = rel
        for m in RE_INTERFACE_DEF.finditer(text):
            interfaces.add(m.group(1))
            known_types.add(m.group(1))
        for m in RE_CLASS.finditer(text):
            name, bases = m.group(1), m.group(2)
            known_types.add(name)
            base_list = [tail(b.strip().split("<")[0])
                         for b in (bases or "").split(",") if b.strip()]
            class_bases.setdefault(name, base_list)

        # Локальные типы ident → тип (для RegisterInstance(ident)).
        ident_types = {m.group(1): m.group(2) for m in RE_VAR_NEW.finditer(text)}

        for m in RE_REGISTER_PAIR.finditer(text):
            svc, impl = tail(m.group(1)), tail(m.group(2))
            provides_by_node[node].append({"service": svc, "impl": impl, "kind": "service"})
            if not is_debug:
                service_keys.update({svc, impl})
                registered_impls.add(impl)
        for m in RE_REGISTER_SELF.finditer(text):
            t = tail(m.group(1))
            provides_by_node[node].append({"service": t, "impl": t, "kind": "self"})
            if not is_debug:
                service_keys.add(t)
                registered_impls.add(t)
        for m in RE_REG_INSTANCE_TYPED.finditer(text):
            t = tail(m.group(1))
            provides_by_node[node].append({"service": t, "impl": t, "kind": "instance"})
            if not is_debug:
                service_keys.add(t)
        for m in RE_REG_INSTANCE_NEW.finditer(text):
            t = tail(m.group(1))
            kind = "config" if t.endswith("Config") else "instance"
            provides_by_node[node].append({"service": t, "impl": t, "kind": kind})
            if not is_debug:
                service_keys.add(t)
        for m in RE_REG_INSTANCE_ID.finditer(text):
            ident = m.group(1)
            t = "EventBus" if ident == "eventBus" else ident_types.get(ident)
            if t is None:
                continue
            kind = "config" if t.endswith("Config") else "instance"
            note = "сама шина событий (хаб)" if t == "EventBus" else None
            entry = {"service": t, "impl": t, "kind": kind}
            if note:
                entry["note"] = note
            provides_by_node[node].append(entry)
            if not is_debug:
                service_keys.add(t)

    return (texts, contracts, local_structs, class_bases, interfaces,
            known_types, registered_impls, provides_by_node, service_keys)


# ────────────────────────── проход 2: использование ─────────────────────────


def scan_usage(texts, known_types, registered_impls):
    usage = {}
    for p, text in texts.items():
        node = node_for(p)
        if node is None:
            continue
        u = usage.setdefault(node, {
            "publishes": defaultdict(list),
            "subscribes": defaultdict(list),
            "requires": defaultdict(set),
            "files": 0,
        })
        u["files"] += 1

        # method-group хендлеры: void OnX(in Type) → карта имя→тип.
        handler_types = {m.group(1): tail(m.group(2))
                         for m in RE_HANDLER.finditer(text)}

        # [Inject] на отдельной строке → склеить с объявлением.
        joined = RE_INJECT_JOIN.sub("[Inject] ", text)

        for m in RE_PUBLISH.finditer(text):
            u["publishes"][tail(m.group(1))].append(p.name)
        for m in RE_SUB_LAMBDA.finditer(text):
            u["subscribes"][tail(m.group(1))].append(p.name)
        for m in RE_SUB_GENERIC.finditer(text):
            u["subscribes"][tail(m.group(1))].append(p.name)
        for m in RE_SUB_METHOD.finditer(text):
            h = handler_types.get(m.group(1))
            if h:
                u["subscribes"][h].append(p.name)

        for m in RE_INJECT_LINE.finditer(joined):
            t = parse_inject_line(m.group(0))
            if t is None:
                continue
            kind, name = split_inject_type(t)
            if kind == "pub":
                u["publishes"].setdefault(name, [])  # канал объявлен
            elif kind == "sub":
                u["subscribes"].setdefault(name, [])
            else:
                u["requires"][name].add("inject")
        for m in RE_RESOLVE.finditer(text):
            t = tail(m.group(1))
            if t != "T":  # generic-параметр хелпера — не требование
                u["requires"][t].add("resolve")
        for m in RE_RESOLVE_ALL.finditer(text):
            u["requires"][tail(m.group(1))].add("resolve-all")

        for m in RE_CTOR.finditer(text):
            if m.group(1) not in registered_impls:
                continue  # ctor вне DI — локальная сборка, не запрос к шине
            for t in parse_ctor_params(m.group(2), known_types):
                u["requires"][t].add("ctor")

    return usage


def clean_requires(u):
    return {svc: sorted(via) for svc, via in u["requires"].items()}


# ────────────────────────────── сборка JSON ────────────────────────────────


def git_head():
    try:
        return subprocess.run(
            ["git", "rev-parse", "--short", "HEAD"], cwd=ROOT,
            capture_output=True, text=True, timeout=10,
        ).stdout.strip()
    except Exception:
        return "unknown"


def make_node(nid, kind, layer, order, description, usage, provides_by_node):
    u = usage.get(nid, {"publishes": {}, "subscribes": {}, "requires": {}, "files": 0})
    requires = clean_requires(u)
    config_names = {p["service"] for p in provides_by_node.get(nid, [])
                    if p["kind"] == "config"}
    req_list = [
        {"service": svc, "via": via,
         "kind": ("config" if svc in config_names else "service")}
        for svc, via in sorted(requires.items())
    ]
    seen = set()
    prov_dedup = []
    for pr in provides_by_node.get(nid, []):
        key = (pr["service"], pr["impl"])
        if key in seen:
            continue
        seen.add(key)
        prov_dedup.append(pr)
    return {
        "id": nid,
        "kind": kind,
        "layer": layer,
        "order": order,
        "description": description,
        "debug": nid == DEBUG_NODE,
        "files": u["files"],
        "publishes": sorted(u["publishes"].keys()),
        "subscribes": sorted(u["subscribes"].keys()),
        "publishes_files": {k: sorted(set(v)) for k, v in sorted(u["publishes"].items())},
        "subscribes_files": {k: sorted(set(v)) for k, v in sorted(u["subscribes"].items())},
        "provides": prov_dedup,
        "requires": req_list,
    }


def build_map():
    (texts, contracts, local_structs, class_bases, interfaces, known_types,
     registered_impls, provides_by_node, service_keys) = scan_universe()
    usage = scan_usage(texts, known_types, registered_impls)

    node_ids = list(MODULE_ORDER) + ["Entry", "Adapter.UI", "Adapter.Scene",
                                     DEBUG_NODE, "Adapter.Input", "Adapter.Persistence"]
    prod_ids = set(node_ids) - {DEBUG_NODE}

    pub_prod, pub_any = defaultdict(set), defaultdict(set)
    sub_prod, sub_any = defaultdict(set), defaultdict(set)
    required_prod = defaultdict(set)
    for nid, u in usage.items():
        for ev in u["publishes"]:
            pub_any[ev].add(nid)
            if nid in prod_ids:
                pub_prod[ev].add(nid)
        for ev in u["subscribes"]:
            sub_any[ev].add(nid)
            if nid in prod_ids:
                sub_prod[ev].add(nid)
        if nid in prod_ids:
            for svc, via in clean_requires(u).items():
                required_prod[svc].add(nid)

    nodes = []
    for idx, mod in enumerate(MODULE_ORDER, start=1):
        nodes.append(make_node(mod, "module", "Modules", idx,
                               MODULE_DESCRIPTIONS[mod], usage, provides_by_node))
    for nid in ["Entry", "Adapter.UI", "Adapter.Scene", "Adapter.Scene.Debug",
                "Adapter.Input", "Adapter.Persistence"]:
        layer, desc = NODE_META[nid]
        kind = "adapter" if nid.startswith("Adapter") else "entry"
        nodes.append(make_node(nid, kind, layer, 100 + len(nodes), desc,
                               usage, provides_by_node))

    provided_prod = {}
    for n in nodes:
        if n["id"] not in prod_ids:
            continue
        for pr in n["provides"]:
            provided_prod.setdefault(pr["service"], {"by": n["id"], "kind": pr["kind"]})

    universe = set(service_keys) | {"IResolver"}  # контейнер self-register

    # Маркеры (resolve-all) резолвятся матчингом инстансов: интерфейс «обеспечен»,
    # если хотя бы один зарегистрированный продакшн-класс его реализует
    # (в т.ч. транзитивно — через базовые классы).
    def base_closure(cls, seen=None):
        seen = seen or set()
        for b in class_bases.get(cls, []):
            if b in seen:
                continue
            seen.add(b)
            base_closure(b, seen)
        return seen

    prod_impls = {i for i in registered_impls}
    marker_resolved = set()
    for iface in interfaces:
        for impl in prod_impls:
            if iface in base_closure(impl):
                marker_resolved.add(iface)
                break

    checks = {
        "events_no_subscriber": [],
        "events_no_publisher": [],
        "contracts_unused": [],
        "contracts_local_unscoped": [],
        "services_unresolved": [],
        "services_unconsumed": [],
    }
    for ev, pubs in sorted(pub_prod.items()):
        if not sub_prod.get(ev):
            checks["events_no_subscriber"].append({
                "event": ev,
                "publishers": sorted(pubs),
                "debug_only": bool(sub_any.get(ev)),
            })
    for ev, subs in sorted(sub_prod.items()):
        if not pub_any.get(ev):
            checks["events_no_publisher"].append({
                "event": ev,
                "subscribers": sorted(subs),
            })
    for name in sorted(contracts):
        if not name.endswith("Event"):
            continue  # DTO-структуры (SaveInfo, HarvestResult) — не сообщения
        if name not in pub_any and name not in sub_any:
            checks["contracts_unused"].append(name)
    for name in sorted(local_structs):
        if (name in pub_any or name in sub_any) and name not in contracts:
            if "SimDebug" in local_structs[name] or "/Audit" in local_structs[name]:
                continue  # локальные типы QA-харнессов
            checks["contracts_local_unscoped"].append(
                {"event": name, "origin": local_structs[name]})
    for svc, nodes_req in sorted(required_prod.items()):
        if svc in universe or svc in marker_resolved:
            continue
        if svc in marker_resolved or svc in interfaces:
            # интерфейс без зарегистрированных impl-ов: маркер без исполнителей
            checks["services_unresolved"].append(
                {"service": svc, "required_by": sorted(nodes_req),
                 "kind": "marker-no-impl"})
            continue
        checks["services_unresolved"].append(
            {"service": svc, "required_by": sorted(nodes_req)})

    marker_consumed = set()
    for nid, u in usage.items():
        if nid not in prod_ids:
            continue
        for svc, via in clean_requires(u).items():
            if "resolve-all" in via:
                marker_consumed.add(svc)

    for svc, info in sorted(provided_prod.items()):
        if info["kind"] != "service":
            continue  # config/self-инстансы не проверяем на «забираемость»
        if svc in required_prod or svc in marker_consumed:
            continue
        impl = next((p["impl"] for n in nodes if n["id"] in prod_ids
                     for p in n["provides"] if p["service"] == svc), None)
        if impl and any(b in marker_consumed for b in class_bases.get(impl, [])):
            continue  # impl реализует потребляемый маркер (ISaveable и т.п.)
        checks["services_unconsumed"].append(
            {"service": svc, "provided_by": info["by"]})

    totals = {
        "modules": len(MODULE_ORDER),
        "nodes": len(nodes),
        "contracts_defined": len(contracts),
        "events_published": len(pub_prod),
        "events_subscribed": len(sub_prod),
        "publish_edges": sum(len(n["publishes"]) for n in nodes),
        "subscribe_edges": sum(len(n["subscribes"]) for n in nodes),
        "provide_edges": sum(len(n["provides"]) for n in nodes),
        "require_edges": sum(len(n["requires"]) for n in nodes),
        "services_provided": len(provided_prod),
        "services_required": len(required_prod),
    }

    hub = {
        "id": "Core",
        "title": "Центральная шина (Core)",
        "channels": [
            {
                "id": "events",
                "title": "EventBus — IPublisher<T> / ISubscriber<T>",
                "role": "события: Publish → Subscribe (readonly struct, zero-GC)",
                "contracts_total": len(contracts),
                "groups": [
                    {"file": f, "count": sum(1 for v in contracts.values() if v == f)}
                    for f in sorted(set(contracts.values()))
                ],
            },
            {
                "id": "services",
                "title": "DI-контейнер — ContainerBuilder / IResolver",
                "role": "сервисы: Register → Resolve / [Inject] / ResolveAll",
                "services_total": len(provided_prod),
            },
        ],
    }

    return {
        "schema_version": SCHEMA_VERSION,
        "updated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "generated_by": "tools/gen_bus_map.py",
        "update_policy": "по требованию: правки кода → python3 tools/gen_bus_map.py → коммит",
        "source_head": git_head(),
        "hub": hub,
        "nodes": nodes,
        "checks": checks,
        "totals": totals,
    }


# ────────────────────────────── main ────────────────────────────────────────


def main():
    ap = argparse.ArgumentParser(description="Генератор карты центральной шины")
    ap.add_argument("--check", action="store_true", help="детектор дрейфа bus_map.json")
    ap.add_argument("--stdout", action="store_true", help="печать в stdout без записи")
    args = ap.parse_args()

    data = build_map()

    if args.stdout:
        json.dump(data, sys.stdout, ensure_ascii=False, indent=2)
        print()
        return 0

    if not args.check:
        OUT.parent.mkdir(parents=True, exist_ok=True)
        OUT.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n",
                       encoding="utf-8")
        t = data["totals"]
        c = data["checks"]
        print(f"[bus-map] {OUT}")
        print(f"[bus-map] узлов: {t['nodes']} (модулей {t['modules']}), "
              f"контрактов: {t['contracts_defined']}, HEAD {data['source_head']}")
        print(f"[bus-map] публикаций: {t['publish_edges']}, подписок: {t['subscribe_edges']}, "
              f"предоставлений: {t['provide_edges']}, запросов: {t['require_edges']}")
        print(f"[bus-map] проверки: без подписчиков {len(c['events_no_subscriber'])}, "
              f"без издателей {len(c['events_no_publisher'])}, "
              f"мёртвые контракты {len(c['contracts_unused'])}, "
              f"неразрешимые {len(c['services_unresolved'])}, "
              f"невостребованные {len(c['services_unconsumed'])}")
        return 0

    if not OUT.exists():
        print("[bus-map] FAIL: portal/data/bus_map.json не существует — сгенерируйте")
        return 1
    old = json.loads(OUT.read_text(encoding="utf-8"))
    for probe in ("nodes", "checks", "hub"):
        if old.get(probe) != data.get(probe):
            print(f"[bus-map] DRIFT: раздел «{probe}» не совпадает с кодом — "
                  f"перегенерируйте: python3 tools/gen_bus_map.py")
            if probe == "checks":
                for key in data["checks"]:
                    o = len(old.get("checks", {}).get(key, []))
                    n = len(data["checks"][key])
                    if o != n:
                        print(f"  - {key}: {o} → {n}")
            return 1
    print("[bus-map] OK: bus_map.json соответствует коду")
    return 0


if __name__ == "__main__":
    sys.exit(main())
