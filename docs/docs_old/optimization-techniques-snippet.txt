/**
 * Оптимизированный код для start/route.ts
 * 
 * Заменяет секции 6 и 7 (техники и формации)
 */

// ============================================
// ВАРИАНТ A: С ПРЕДСОЗДАНИЕМ (рекомендуется)
// ============================================
// Предварительно выполнить: bun run db:seed-techniques
// Техники уже существуют в БД, нужно только создать связи

// 6. Создаём связи с базовыми техниками (БЕЗ upsert!)
const techniqueNameIds = BASIC_TECHNIQUES.map(p => p.id);
const formationNameIds = BASIC_FORMATIONS.map(p => p.id);

// Получаем все ID техник ОДНИМ запросом
const existingTechniques = await tx.technique.findMany({
  where: {
    nameId: { in: [...techniqueNameIds, ...formationNameIds] }
  },
  select: { id: true, nameId: true }
});

const techniqueIdMap = new Map(
  existingTechniques.map(t => [t.nameId, t.id])
);

// Пакетная вставка связей
const characterTechniquesData = [];

for (const preset of BASIC_TECHNIQUES) {
  const techniqueId = techniqueIdMap.get(preset.id);
  if (techniqueId) {
    characterTechniquesData.push({
      characterId: character.id,
      techniqueId,
      mastery: 0,
      learningProgress: 100,
      learningSource: "preset",
    });
  }
}

for (const preset of BASIC_FORMATIONS) {
  const techniqueId = techniqueIdMap.get(preset.id);
  if (techniqueId) {
    characterTechniquesData.push({
      characterId: character.id,
      techniqueId,
      mastery: 0,
      learningProgress: 100,
      learningSource: "preset",
    });
  }
}

// Создаём все связи ОДНИМ запросом
await tx.characterTechnique.createMany({
  data: characterTechniquesData,
  skipDuplicates: true,
});

// Результат: 2 запроса вместо 20!
// 1. findMany (получить ID техник) - ~10ms
// 2. createMany (создать связи) - ~30ms
// Итого: ~40ms вместо ~700ms

// ============================================
// ВАРИАНТ B: БЕЗ ПРЕДСОЗДАНИЯ (fallback)
// ============================================
// Если техники ещё не созданы - создаём их
// Но тоже с пакетной вставкой!

// Проверяем существующие
const existingNameIds = existingTechniques.map(t => t.nameId);

const techniquesToCreate = [...BASIC_TECHNIQUES, ...BASIC_FORMATIONS]
  .filter(p => !existingNameIds.includes(p.id));

if (techniquesToCreate.length > 0) {
  // Пакетное создание техник
  await tx.technique.createMany({
    data: techniquesToCreate.map(preset => ({
      name: preset.name,
      nameId: preset.id,
      description: preset.description,
      type: preset.techniqueType || 'formation',
      element: preset.element || 'neutral',
      rarity: preset.rarity,
      level: preset.level || 1,
      minLevel: preset.minLevel || 1,
      maxLevel: preset.maxLevel || preset.qualityLevels || 9,
      canEvolve: preset.canEvolve ?? true,
      minCultivationLevel: preset.minCultivationLevel || 1,
      qiCost: preset.qiCost || 0,
      physicalFatigueCost: preset.fatigueCost?.physical || 0,
      mentalFatigueCost: preset.fatigueCost?.mental || 0,
      statRequirements: preset.statRequirements ? JSON.stringify(preset.statRequirements) : null,
      statScaling: preset.scaling ? JSON.stringify(preset.scaling) : null,
      effects: preset.effects ? JSON.stringify(preset.effects) : null,
      source: "preset",
    })),
    skipDuplicates: true,
  });
  
  // Получаем ID для новых техник
  const newTechniques = await tx.technique.findMany({
    where: { nameId: { in: techniquesToCreate.map(p => p.id) } },
    select: { id: true, nameId: true }
  });
  
  newTechniques.forEach(t => techniqueIdMap.set(t.nameId, t.id));
}

// Затем пакетная вставка связей (как в Варианте A)
