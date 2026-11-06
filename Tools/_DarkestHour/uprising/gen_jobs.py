"""Должности режима «Восстание» (Лост-Энджелс): job, иконка, удостоверение, точка появления, playTimeTracker и
локализация (en/ru) — из одной таблицы. Снаряжение — в Resources/Prototypes/_DarkestHour/Uprising/gear.yml (руками).

Запуск из корня репозитория: python3 Tools/_DarkestHour/uprising/gen_jobs.py"""
import os
import re

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..')
P = ROOT + '/Resources/Prototypes/_DarkestHour/Uprising/jobs.yml'
EN = ROOT + '/Resources/Locale/en-US/_DarkestHour/uprising_jobs.ftl'
RU = ROOT + '/Resources/Locale/ru-RU/_DarkestHour/uprising_jobs.ftl'

# (id, отдел, иконка, слоты, доступ, en имя, ru имя, en описание, ru описание, удостоверение как у (job) или None)
DEPTS = {
    'DHLACivilians': ('#8a8a8a', 'Civilians of Lost Angels', 'Жители Лост-Энджелса',
                      'People of the district: workers, officials, shopkeepers and residents.',
                      'Люди квартала: рабочие, чиновники, лавочники и жители.'),
    'DHLAVanguard': ('#7a5cc4', 'Lost Angels—Vanguard', 'Lost Angels—Vanguard',
                     'The LA-V corporate office in the Gates business centre.',
                     'Офис корпорации LA-V в бизнес-центре «Ворота».'),
    'DHLAMilitaryPolice': ('#3f5f9e', 'Military Police', 'Военная Полиция',
                           'The Republican Military Police district station.',
                           'Районное отделение Военной Полиции Республики.'),
    'DHLAFront': ('#c8322b', 'Liberation Front of Free Colonies', 'Освободительный Фронт Свободных Колоний',
                  'LFFC cells hiding among the residents.',
                  'Ячейки ОФСК, скрывающиеся среди жителей.'),
}

JOBS = [
    # мирные
    ('DHLADistrictHead', 'DHLACivilians', 'HeadOfPersonnel', 1, ['DHLADistrict'],
     'District head', 'глава района',
     'Run the district office and keep the district alive through the unrest.',
     'Руководите управой и сохраните район, пока на улицах неспокойно.', None),
    ('DHLADistrictStaff', 'DHLACivilians', 'Lawyer', 3, ['DHLADistrict'],
     'District official', 'сотрудник управы',
     'Work in the district office: papers, residents, emergencies.',
     'Работайте в управе: документы, жители, происшествия.', None),
    ('DHLAOfficeWorker', 'DHLACivilians', 'Visitor', 6, [],
     'Office worker', 'офисный работник',
     'Work in the offices of the Gates business centre.',
     'Работайте в офисах бизнес-центра «Ворота».', None),
    ('DHLAEntrepreneur', 'DHLACivilians', 'Bartender', 4, [],
     'Small business owner', 'мелкий предприниматель',
     'Run your shop, cafe or stall and keep it from being looted.',
     'Держите свою лавку, кафе или ларёк и не дайте их разграбить.', None),
    ('DHLAFactoryWorker', 'DHLACivilians', 'CargoTechnician', 6, ['DHLAFactory'],
     'Factory worker', 'рабочий завода',
     'Work at the district factory and warehouse.',
     'Работайте на заводе и складе квартала.', None),
    ('DHLAUtilityWorker', 'DHLACivilians', 'StationEngineer', 4, ['DHLAUtility'],
     'Utility worker', 'коммунальщик',
     'Keep the substation, the sewers and the water running.',
     'Следите за подстанцией, канализацией и водой.', None),
    ('DHLAMetroWorker', 'DHLACivilians', 'TramDriver', 2, ['DHLAMetro'],
     'Metro worker', 'работник метро',
     'Keep the metro station on the square working.',
     'Обслуживайте станцию метро на площади.', None),
    ('DHLAResident', 'DHLACivilians', 'Passenger', 20, [],
     'Resident', 'житель',
     'Live through another day in the district.',
     'Переживите ещё один день в квартале.', None),
    # LA-V
    ('DHLAVRepresentative', 'DHLAVanguard', 'dh:lav_representative', 1, ['DHLAVanguard', 'DHLAVanguardOffice'],
     'Corporate representative', 'представитель корпорации',
     'Represent LA-V in the district. Do business with whoever pays, and leave alive through LZ-1 Aeronautics.',
     'Представляйте LA-V в районе. Ведите дела с тем, кто платит, и улетите живым через ЗП-1 «Аэронавтика».', None),
    ('DHLAVAssistant', 'DHLAVanguard', 'dh:lav_assistant', 1, ['DHLAVanguard', 'DHLAVanguardOffice'],
     "Representative's assistant", 'помощник представителя',
     'Assist the corporate representative and run the office.',
     'Помогайте представителю корпорации и ведите дела офиса.', None),
    ('DHLAVSecurity', 'DHLAVanguard', 'dh:lav_security', 2, ['DHLAVanguard', 'DHLAVanguardOffice'],
     'LA-V security officer', 'офицер СБ LA-V',
     'Guard the representative and the LA-V office.',
     'Охраняйте представителя и офис LA-V.', None),
    ('DHLAVEmployee', 'DHLAVanguard', 'dh:lav_employee', 2, ['DHLAVanguard'],
     'LA-V employee', 'сотрудник LA-V',
     'Work in the LA-V office.',
     'Работайте в офисе LA-V.', None),
    # военпол (решение автора 2026-11-05): кадет, офицер, следователь, зам. начальника, начальник, надзиратель
    ('DHLAMPChief', 'DHLAMilitaryPolice', 'HeadOfSecurity', 1, ['DHLAPolice', 'DHLAPoliceCommand'],
     'Military Police chief', 'начальник Военной Полиции',
     'Command the district Military Police: find the LFFC leaders and protect the residents.',
     'Командуйте отделением Военной Полиции: найдите лидеров ОФСК и защитите жителей.', None),
    ('DHLAMPDeputyChief', 'DHLAMilitaryPolice', 'SeniorOfficer', 1, ['DHLAPolice', 'DHLAPoliceCommand'],
     'Deputy chief of the Military Police', 'заместитель начальника Военной Полиции',
     'Lead the patrols in the field and stand in for the chief.',
     'Ведите патрули в поле и замещайте начальника.', None),
    ('DHLAMPInvestigator', 'DHLAMilitaryPolice', 'Detective', 2, ['DHLAPolice'],
     'Military Police investigator', 'следователь Военной Полиции',
     'Investigate, interrogate the detained and find out who leads the Front.',
     'Расследуйте, допрашивайте задержанных и выясните, кто стоит во главе Фронта.', None),
    ('DHLAMPWarden', 'DHLAMilitaryPolice', 'Warden', 2, ['DHLAPolice', 'DHLAPoliceCommand'],
     'Military Police prison warden', 'тюремный надзиратель Военной Полиции',
     'Guard the cells of the Military Police station and the detained in them.',
     'Охраняйте камеры отделения Военной Полиции и задержанных в них.', None),
    ('DHLAMPOfficer', 'DHLAMilitaryPolice', 'SecurityOfficer', 8, ['DHLAPolice'],
     'Military Police officer', 'офицер Военной Полиции',
     'Patrol the district, detain the LFFC fighters and do not shoot civilians.',
     'Патрулируйте район, задерживайте боевиков ОФСК и не стреляйте в мирных.', None),
    ('DHLAMPCadet', 'DHLAMilitaryPolice', 'SecurityCadet', 3, ['DHLAPolice'],
     'Military Police cadet', 'кадет Военной Полиции',
     'Learn the job from the officers. Rubber rounds first.',
     'Учитесь у офицеров. Сначала — резиновые пули.', None),
    # ОФСК — удостоверения как у жителей (решение автора 2026-11-05): глава отряда, диверсант, специалист,
    # пулемётчик, повстанец; оружие — ОНК
    ('DHLAFrontLeader', 'DHLAFront', 'dh:front_leader', 2, [],
     'LFFC squad leader', 'глава отряда ОФСК',
     'Lead the cell: hold the district, blow up the factory or broadcast the manifesto. Hide among the residents.',
     'Ведите ячейку: удержите квартал, взорвите завод или выйдите в эфир с манифестом. Прячьтесь среди жителей.',
     'DHLAResident'),
    ('DHLAFrontSaboteur', 'DHLAFront', 'dh:front_saboteur', 2, [],
     'LFFC saboteur', 'диверсант ОФСК',
     'Blow up what the squad leader points at. Hide among the residents.',
     'Взрывайте то, на что укажет глава отряда. Прячьтесь среди жителей.', 'DHLAResident'),
    ('DHLAFrontSpecialist', 'DHLAFront', 'dh:front_specialist', 2, [],
     'LFFC specialist', 'специалист ОФСК',
     'A marksman of the Front: hit officers and gunners from afar. Hide among the residents.',
     'Стрелок Фронта: поражайте офицеров и пулемётчиков издалека. Прячьтесь среди жителей.', 'DHLAResident'),
    ('DHLAFrontMachinegunner', 'DHLAFront', 'dh:front_machinegunner', 2, [],
     'LFFC machine gunner', 'пулемётчик ОФСК',
     'Hold the street with the Firestarter-2T. Hide among the residents.',
     'Держите улицу из Firestarter-2T. Прячьтесь среди жителей.', 'DHLAResident'),
    ('DHLAFrontRebel', 'DHLAFront', 'dh:front_rebel', 10, [],
     'LFFC rebel', 'повстанец ОФСК',
     'Fight for the Front and follow your squad leader. Hide among the residents.',
     'Сражайтесь за Фронт и слушайтесь главу отряда. Прячьтесь среди жителей.', 'DHLAResident'),
]

DH_ICONS = '/Textures/_DarkestHour/Interface/uprising_job_icons.rsi'
CARD_BASE = {'DHLACivilians': 'DHLAPassportBase', 'DHLAVanguard': 'DHLAVIDCardBase',
             'DHLAMilitaryPolice': 'DHDogtags', 'DHLAFront': 'DHLAPassportBase'}
LAV_CARD = {'DHLAVRepresentative': 'corporate_liaison', 'DHLAVAssistant': 'corporate_liaison',
            'DHLAVSecurity': 'pmc', 'DHLAVEmployee': 'silver'}

ACCESS = [
    ('DHLADistrict', 'District office', 'управа'),
    ('DHLAFactory', 'Factory and warehouse', 'завод и склад'),
    ('DHLAUtility', 'Utilities', 'коммунальные службы'),
    ('DHLAMetro', 'Metro', 'метро'),
    ('DHLAVanguard', 'LA-V basic', 'LA-V базовый'),
    ('DHLAVanguardOffice', 'LA-V command', 'LA-V командный'),
    ('DHLAPolice', 'Military Police', 'Военная Полиция'),
    ('DHLAPoliceCommand', 'Military Police command', 'командование Военной Полиции'),
]


def kebab(s):
    return re.sub(r'(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])', '-', s).lower()


y, en, ru = [], [], []
w = y.append
w('# Генерируется Tools/_DarkestHour/uprising/gen_jobs.py — правьте таблицу там.')
w('# «Восстание» (решение автора 2026-11-05): квартал Лост-Энджелса. Мирные жители, LA-V (Lost Angels—Vanguard),')
w('# Военная Полиция Республики и ОФСК (прячется среди жителей: удостоверение — как у жителя).')
w('')
for aid, aen, aru in ACCESS:
    key = 'id-card-access-level-' + kebab(aid)
    w('- type: accessLevel')
    w('  id: %s' % aid)
    w('  name: %s' % key)
    w('')
    en.append('%s = %s' % (key, aen))
    ru.append('%s = %s' % (key, aru))

# вкладки окна персонажа: режим «Восстание», стороны (решение автора 2026-11-06)
SIDES = {'DHLAMilitaryPolice': ('dh-jobs-side-military-police', 1), 'DHLACivilians': ('dh-jobs-side-civilians', 2),
         'DHLAVanguard': ('dh-jobs-side-corporates', 3), 'DHLAFront': ('dh-jobs-side-rebels', 4)}

for dept, (color, den, dru, dden, ddru) in DEPTS.items():
    key = 'dh-department-' + kebab(dept)
    w('- type: department')
    w('  id: %s' % dept)
    w('  name: %s' % key)
    w('  description: %s-description' % key)
    w('  color: "%s"' % color)
    w('  weight: 80')
    w('  dhMode: dh-jobs-mode-uprising')
    w('  dhModeOrder: 2')
    w('  dhSide: %s' % SIDES[dept][0])
    w('  dhSideOrder: %d' % SIDES[dept][1])
    w('  roles:')
    for j in JOBS:
        if j[1] == dept:
            w('  - %s' % j[0])
    w('')
    en += ['%s = %s' % (key, den), '%s-description = %s' % (key, dden)]
    ru += ['%s = %s' % (key, dru), '%s-description = %s' % (key, ddru)]

for jid, dept, icon, slots, access, nen, nru, den, dru, card_as in JOBS:
    k = 'dh-job-' + kebab(jid[2:])
    w('- type: job')
    w('  id: %s' % jid)
    w('  name: %s-name' % k)
    w('  description: %s-desc' % k)
    w('  playTimeTracker: Job%s' % jid)
    w('  startingGear: %sGear' % jid)
    w('  icon: JobIcon%s' % jid)
    w('  supervisors: dh-job-%s-supervisors' % kebab(dept[2:]))
    if access:
        w('  access:')
        for a in access:
            w('  - %s' % a)
    w('')
    w('- type: playTimeTracker')
    w('  id: Job%s' % jid)
    w('')
    # Иконка должности — своя у всех (ОФСК и LA-V — свои рисованные, решение автора 2026-11-05).
    sprite_path, state = (DH_ICONS, icon[3:]) if icon.startswith('dh:') else ('/Textures/Interface/Misc/job_icons.rsi', icon)
    w('- type: jobIcon')
    w('  parent: JobIcon')
    w('  id: JobIcon%s' % jid)
    w('  icon:')
    w('    sprite: %s' % sprite_path)
    w('    state: %s' % state)
    w('  jobName: %s-name' % k)
    w('')
    # Документ: мирным — паспорт, LA-V — корпоративная карта, военполу — жетоны; ОФСК — паспорт жителя.
    if card_as is None:
        w('- type: entity')
        w('  parent: %s' % CARD_BASE[dept])
        w('  id: DHLAIDCard%s' % jid[4:])
        w('  suffix: %s' % nen)
        w('  components:')
        if dept == 'DHLAVanguard':
            card_state = LAV_CARD.get(jid, 'silver')
            w('  - type: Sprite')
            w('    layers:')
            w('    - state: %s' % card_state)
        w('  - type: PresetIdCard')
        w('    job: %s' % jid)
        w('')
    w('- type: entity')
    w('  parent: SpawnPointJobBase')
    w('  id: SpawnPoint%s' % jid)
    w('  name: %s' % nen.lower())
    w('  components:')
    w('  - type: SpawnPoint')
    w('    job_id: %s' % jid)
    w('  - type: Sprite')
    w('    layers:')
    w('    - state: green')
    w('    - sprite: %s' % sprite_path.replace('/Textures/', ''))
    w('      state: %s' % state)
    w('')
    en += ['%s-name = %s' % (k, nen), '%s-desc = %s' % (k, den)]
    ru += ['%s-name = %s' % (k, nru), '%s-desc = %s' % (k, dru)]

for dept, en_s, ru_s in [('DHLACivilians', 'the district head', 'глава района'),
                         ('DHLAVanguard', 'the LA-V board of directors', 'совет директоров LA-V'),
                         ('DHLAMilitaryPolice', 'the Military Police command', 'командование Военной Полиции'),
                         ('DHLAFront', 'the squad leader', 'глава отряда')]:
    en.append('dh-job-%s-supervisors = %s' % (kebab(dept[2:]), en_s))
    ru.append('dh-job-%s-supervisors = %s' % (kebab(dept[2:]), ru_s))

os.makedirs(os.path.dirname(P), exist_ok=True)
open(P, 'w', encoding='utf-8').write('\n'.join(y).rstrip('\n') + '\n')
open(EN, 'w', encoding='utf-8').write('\n'.join(en) + '\n')
open(RU, 'w', encoding='utf-8').write('\n'.join(ru) + '\n')

# Русские названия сущностей (точки появления, удостоверения)
rp = ROOT + '/Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Uprising/jobs.ftl'
os.makedirs(os.path.dirname(rp), exist_ok=True)
lines = []
for jid, dept, icon, slots, access, nen, nru, den, dru, card_as in JOBS:
    lines.append('ent-SpawnPoint%s = %s\n    .suffix = точка появления' % (jid, nru))
    if card_as is None:
        base = CARD_BASE[dept]
        lines.append('ent-DHLAIDCard%s = { ent-%s }\n    .desc = { ent-%s.desc }\n    .suffix = %s' % (jid[4:], base, base, nru))
open(rp, 'w', encoding='utf-8').write('\n\n'.join(lines) + '\n')

# слоты станции — для uprising.yml
print('\n'.join('          %s: [ %d, %d ]' % (j[0], j[3], j[3]) for j in JOBS))
