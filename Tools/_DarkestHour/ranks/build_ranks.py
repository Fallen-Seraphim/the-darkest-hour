#!/usr/bin/env python3
"""DarkestHour: звания (решения автора 2026-11-13) — Республика (КРА), Военная полиция, Первая Империя.

Механика — система званий RMC-14 (RankSystem): звание перед именем в чате и рации, при осмотре — с кодом
(E3, O2, IM4, IFO1), растёт с наигранным временем на должности. Знаки различия на форме и броне — шевроны и погоны
CMU/cmss13: Республика — шевроны морпехов США и офицерские знаки, ВП — шевроны армии США, Империя — погоны ИТМ.

Запуск из корня репозитория: python3 Tools/_DarkestHour/ranks/build_ranks.py
Пишет Resources/Prototypes/_DarkestHour/Ranks/ranks.yml, локализацию en-US/ru-RU (_DarkestHour/ranks.ftl) и блоки
ranks: у должностей в Assault/jobs.yml и Uprising/jobs.yml (между маркерами, повторный запуск безопасен).
"""
import re

CH = '_CMU14/Clothing/Accessory/Chevrons'
USCM, OFF, USA, UNI = f'{CH}/uscm.rsi', f'{CH}/officers.rsi', f'{CH}/usarmy.rsi', f'{CH}/universal.rsi'

# id: (код, ru, ru сокр., en, en сокр., rsi, состояние на теле)
RANKS = {
    # --- Корпус Республиканской Армии
    'DHRankRepublicPrivate': ('E1', 'Рядовой', 'Ряд.', 'Private', 'Pvt.', USCM, 'me1'),
    'DHRankRepublicPrivateFirstClass': ('E2', 'Рядовой первого класса', 'Ряд. 1 кл.', 'Private First Class', 'PFC', USCM, 'me2'),
    'DHRankRepublicLanceCorporal': ('E3', 'Младший капрал', 'Мл. капр.', 'Lance Corporal', 'LCpl.', USCM, 'me3'),
    'DHRankRepublicCorporal': ('E4', 'Капрал', 'Капр.', 'Corporal', 'Cpl.', USCM, 'me4'),
    'DHRankRepublicSergeant': ('E5', 'Сержант', 'Серж.', 'Sergeant', 'Sgt.', USCM, 'me5'),
    'DHRankRepublicStaffSergeant': ('E6', 'Штаб-сержант', 'Шт.-серж.', 'Staff Sergeant', 'SSgt.', USCM, 'me6'),
    'DHRankRepublicSecondLieutenant': ('O1', 'Второй лейтенант', '2-й лейт.', 'Second Lieutenant', '2ndLt.', OFF, 'o1'),
    'DHRankRepublicFirstLieutenant': ('O2', 'Первый лейтенант', '1-й лейт.', 'First Lieutenant', '1stLt.', OFF, 'o2'),
    'DHRankRepublicCaptain': ('O3', 'Капитан', 'Кап.', 'Captain', 'Capt.', OFF, 'o3'),
    'DHRankRepublicMajor': ('O4', 'Майор', 'Майор', 'Major', 'Maj.', OFF, 'o4'),
    'DHRankRepublicLieutenantColonel': ('O5', 'Подполковник', 'Подп.', 'Lieutenant Colonel', 'LtCol.', OFF, 'o5'),
    'DHRankRepublicColonel': ('O6', 'Полковник', 'Полк.', 'Colonel', 'Col.', OFF, 'o6'),
    # --- Военная полиция Республики
    'DHRankMPPrivate': ('E1', 'Рядовой Военной полиции', 'Ряд. ВП', 'Military Police Private', 'Pvt. MP', USA, 'ae1'),
    'DHRankMPCorporal': ('E4', 'Капрал Военной полиции', 'Капр. ВП', 'Military Police Corporal', 'Cpl. MP', USA, 'ae4'),
    'DHRankMPSergeant': ('E5', 'Сержант Военной полиции', 'Серж. ВП', 'Military Police Sergeant', 'Sgt. MP', USA, 'ae5'),
    'DHRankMPStaffSergeant': ('E6', 'Штаб-сержант Военной полиции', 'Шт.-серж. ВП', 'Military Police Staff Sergeant', 'SSgt. MP', USA, 'ae6'),
    'DHRankMPWarrantOfficer': ('W1', 'Уорент-офицер Военной полиции', 'Уор.-оф. ВП', 'Military Police Warrant Officer', 'WO MP', UNI, 'warrant'),
    'DHRankMPChiefWarrantOfficer': ('W2', 'Старший уорент-офицер Военной полиции', 'Ст. уор.-оф. ВП', 'Military Police Chief Warrant Officer', 'CWO MP', UNI, 'warrant'),
    'DHRankMPCaptain': ('O3', 'Капитан Военной полиции', 'Кап. ВП', 'Military Police Captain', 'Capt. MP', OFF, 'o3'),
    'DHRankMPMajor': ('O4', 'Майор Военной полиции', 'Майор ВП', 'Military Police Major', 'Maj. MP', OFF, 'o4'),
    'DHRankMPLieutenantColonel': ('O5', 'Подполковник Военной полиции', 'Подп. ВП', 'Military Police Lieutenant Colonel', 'LtCol. MP', OFF, 'o5'),
    # --- Имперские морпехи (IM / IMO)
    'DHRankImperialMarine': ('IM1', 'Морпех', 'Мрп.', 'Marine', 'Mne.', UNI, 'enlisted'),
    'DHRankImperialLanceCorporal': ('IM2', 'Ефрейтор', 'Ефр.', 'Lance Corporal', 'LCpl.', UNI, 'enlisted'),
    'DHRankImperialJuniorInfantryOfficer': ('IM3', 'Младший пехотный офицер', 'Мл. пех. оф.', 'Junior Infantry Officer', 'JIO', UNI, 'nco'),
    'DHRankImperialInfantryOfficer': ('IM4', 'Пехотный офицер', 'Пех. оф.', 'Infantry Officer', 'IO', UNI, 'snco'),
    'DHRankImperialCommandor': ('IM5', 'Коммандор', 'Комм.', 'Commandor', 'Cmdr.', UNI, 'warrant'),
    'DHRankImperialCaptain': ('IMO3', 'Капитан', 'Кап.', 'Captain', 'Capt.', UNI, 'officer'),
    'DHRankImperialFlagCaptain': ('IMO4', 'Флаг-капитан', 'Флаг-кап.', 'Flag Captain', 'F.Capt.', UNI, 'officer'),
    'DHRankImperialMajor': ('IMO5', 'Майор', 'Майор', 'Major', 'Maj.', UNI, 'flagofficer'),
    'DHRankImperialFlagMajor': ('IMO6', 'Флаг-майор', 'Флаг-майор', 'Flag Major', 'F.Maj.', UNI, 'flagofficer'),
    # --- Имперский флот (IF / IFO)
    'DHRankImperialSeaman': ('IF1', 'Матрос', 'Мтр.', 'Seaman', 'Smn.', UNI, 'enlisted'),
    'DHRankImperialSeniorSeaman': ('IF2', 'Старший матрос', 'Ст. мтр.', 'Senior Seaman', 'Sr.Smn.', UNI, 'enlisted'),
    'DHRankImperialMasterSeaman': ('IF3', 'Мастер-матрос', 'Маст.-мтр.', 'Master Seaman', 'M.Smn.', UNI, 'nco'),
    'DHRankImperialFleetLieutenant': ('IFO1', 'Лейтенант', 'Лейт.', 'Lieutenant', 'Lt.', UNI, 'officer'),
    'DHRankImperialFlagLieutenant': ('IFO2', 'Флаг-лейтенант', 'Флаг-лейт.', 'Flag Lieutenant', 'F.Lt.', UNI, 'officer'),
    'DHRankImperialCaptain3': ('IFO3', 'Капитан 3-го ранга', 'Кап. 3 р.', 'Captain Third Rank', 'Capt.3', UNI, 'officer'),
    'DHRankImperialCaptain2': ('IFO4', 'Капитан 2-го ранга', 'Кап. 2 р.', 'Captain Second Rank', 'Capt.2', UNI, 'flagofficer'),
    'DHRankImperialCaptain1': ('IFO5', 'Капитан 1-го ранга', 'Кап. 1 р.', 'Captain First Rank', 'Capt.1', UNI, 'flagofficer'),
}

H = 3600
# должность: [(звание, часов на должности), …] — от высшего к низшему, последнее — без требований
R = 'DHRankRepublic'
I = 'DHRankImperial'
M = 'DHRankMP'
JOBS = {}
for job in ('DHGarrisonRifleman', 'DHGarrisonMachinegunner', 'DHGarrisonSniper', 'DHGarrisonFlamer', 'DHGarrisonAAGunner',
            'DHGarrisonSapper', 'DHGarrisonMortarOperator', 'DHGarrisonFieldArtillerist', 'DHGarrisonTankCrew',
            'DHGarrisonAPCCrew'):
    JOBS[job] = [(R + 'LanceCorporal', 50), (R + 'PrivateFirstClass', 10), (R + 'Private', 0)]
JOBS['DHGarrisonMedic'] = [(R + 'Corporal', 25), (R + 'PrivateFirstClass', 0)]
for job in ('DHGarrisonMortarCommander', 'DHGarrisonTankCommander', 'DHGarrisonAPCCommander'):
    JOBS[job] = [(R + 'StaffSergeant', 70), (R + 'Sergeant', 25), (R + 'Corporal', 0)]
JOBS['DHGarrisonOfficer'] = [(R + 'FirstLieutenant', 25), (R + 'SecondLieutenant', 0)]
JOBS['DHGarrisonArtilleryOfficer'] = [(R + 'Captain', 25), (R + 'FirstLieutenant', 0)]
JOBS['DHGarrisonCommander'] = [(R + 'Colonel', 100), (R + 'LieutenantColonel', 30), (R + 'Major', 0)]
for job in ('DHMarineRifleman', 'DHMarineMachinegunner', 'DHMarineWeaponsSpecialist', 'DHMarineMortarOperator',
            'DHMarineFieldArtillerist', 'DHMarineTankCrew', 'DHMarineAPCCrew'):
    JOBS[job] = [(I + 'JuniorInfantryOfficer', 50), (I + 'LanceCorporal', 10), (I + 'Marine', 0)]
JOBS['DHMarineFieldMedic'] = [(I + 'JuniorInfantryOfficer', 25), (I + 'LanceCorporal', 0)]
for job in ('DHMarineMortarCommander', 'DHMarineTankCommander', 'DHMarineAPCCommander'):
    JOBS[job] = [(I + 'Commandor', 25), (I + 'InfantryOfficer', 0)]
for job in ('DHCarrierArtillerist', 'DHCarrierTechnician'):
    JOBS[job] = [(I + 'MasterSeaman', 50), (I + 'SeniorSeaman', 10), (I + 'Seaman', 0)]
for job in ('DHCarrierPilot', 'DHCarrierAttackPilot'):
    JOBS[job] = [(I + 'FlagLieutenant', 25), (I + 'FleetLieutenant', 0)]
JOBS['DHCarrierSupportOfficer'] = [(I + 'Captain3', 25), (I + 'FlagLieutenant', 0)]
JOBS['DHMarineCommander'] = [(I + 'FlagCaptain', 50), (I + 'Captain', 0)]
JOBS['DHCarrierCaptain'] = [(I + 'Captain1', 50), (I + 'Captain2', 0)]
JOBS['DHCarrierOperationCommander'] = [(I + 'FlagMajor', 100), (I + 'Major', 0)]
JOBS['DHLAMPCadet'] = [(M + 'Private', 0)]
JOBS['DHLAMPOfficer'] = [(M + 'Sergeant', 25), (M + 'Corporal', 0)]
JOBS['DHLAMPWarden'] = [(M + 'StaffSergeant', 0)]
JOBS['DHLAMPInvestigator'] = [(M + 'ChiefWarrantOfficer', 50), (M + 'WarrantOfficer', 0)]
JOBS['DHLAMPDeputyChief'] = [(M + 'Captain', 0)]
JOBS['DHLAMPChief'] = [(M + 'LieutenantColonel', 50), (M + 'Major', 0)]

JOB_FILES = ['Resources/Prototypes/_DarkestHour/Assault/jobs.yml', 'Resources/Prototypes/_DarkestHour/Uprising/jobs.yml']
BEGIN, END = '  # ranks: build_ranks.py', '  # /ranks'


def key(rank_id, part):
    return 'dh-rank-' + re.sub(r'(?<!^)(?=[A-Z0-9])', '-', rank_id[len('DHRank'):]).lower() + part


def main():
    y = ['# Сгенерировано Tools/_DarkestHour/ranks/build_ranks.py — правьте генератор.\n',
         '# Звания «Тёмного часа» (решения автора 2026-11-13). Знаки различия — шевроны и погоны CMU/cmss13.\n']
    en, ru = ['# Звания (Tools/_DarkestHour/ranks/build_ranks.py)'], ['# Звания (Tools/_DarkestHour/ranks/build_ranks.py)']
    for rid, (grade, ru_n, ru_p, en_n, en_p, rsi, state) in RANKS.items():
        y.append(f'\n- type: rank\n  id: {rid}\n  name: {key(rid, "")}\n  prefix: {key(rid, "-prefix")}\n'
                 f'  paygrade: {grade}\n  insignia:\n    sprite: {rsi}\n    state: equipped-ranks_{state}\n')
        en += [f'{key(rid, "")} = {en_n}', f'{key(rid, "-prefix")} = {en_p}']
        ru += [f'{key(rid, "")} = {ru_n}', f'{key(rid, "-prefix")} = {ru_p}']
    common_en = ['rmc-rank-component-examine = { CAPITALIZE(SUBJECT($user)) } { GENDER($user) ->',
                 '    [epicene] hold', '   *[other] holds', '  } the rank of [color=white]{ $rank }[/color].',
                 'dh-rank-insignia-hide-verb = Hide insignia', 'dh-rank-insignia-show-verb = Show insignia']
    common_ru = ['rmc-rank-component-examine = Звание: [color=white]{ $rank }[/color].',
                 'dh-rank-insignia-hide-verb = Спрятать знаки различия', 'dh-rank-insignia-show-verb = Показать знаки различия']
    open('Resources/Prototypes/_DarkestHour/Ranks/ranks.yml', 'w', encoding='utf-8').write(''.join(y))
    open('Resources/Locale/en-US/_DarkestHour/ranks.ftl', 'w', encoding='utf-8').write('\n'.join(common_en + en) + '\n')
    open('Resources/Locale/ru-RU/_DarkestHour/ranks.ftl', 'w', encoding='utf-8').write('\n'.join(common_ru + ru) + '\n')

    done = set()
    for path in JOB_FILES:
        text = open(path, encoding='utf-8').read()
        text = re.sub(rf'{re.escape(BEGIN)}\n(?:.*\n)*?{re.escape(END)}\n', '', text)
        for job, ranks in JOBS.items():
            m = re.search(rf'^- type: job\n  id: {job}\n((?:  .*\n)*)', text, re.M)
            if not m:
                continue
            tracker = re.search(r'  playTimeTracker: (\S+)', m.group(1)).group(1)
            block = [BEGIN, '  ranks:']
            for rank, hours in ranks:
                assert rank in RANKS, rank
                if hours:
                    block += [f'    {rank}:', '    - !type:RoleTimeRequirement', f'      role: {tracker}',
                              f'      time: {hours * H} # {hours} ч']
                else:
                    block.append(f'    {rank}: []')
            block.append(END)
            end = m.end()
            text = text[:end] + '\n'.join(block) + '\n' + text[end:]
            done.add(job)
        open(path, 'w', encoding='utf-8').write(text)
    missing = set(JOBS) - done
    assert not missing, missing
    print('званий', len(RANKS), 'должностей', len(done))


if __name__ == '__main__':
    main()
