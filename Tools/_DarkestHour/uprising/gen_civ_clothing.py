"""Гражданская одежда Лост-Энджелса из RMC-14 и лоадауты мирных должностей (решение автора 2026-11-06).

- копирует RSI из RMC-14 в Resources/Textures/_RMC14/... (тот же путь);
- пишет вещи, «случайные» заготовки (DHRandomOutfit) и лоадауты в Resources/Prototypes/_DarkestHour/Uprising/civ_clothing.yml;
- русские названия — Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Uprising/civ_clothing.ftl.

В лоадауте у каждой должности первым идёт её привычный комплект (выбран по умолчанию), затем «случайное» и все вещи.
Слоты, которые закрывает лоадаут, из стартового снаряжения (Uprising/gear.yml) убраны.

python3 Tools/_DarkestHour/uprising/gen_civ_clothing.py /path/to/RMC-14
"""
import json
import os
import shutil
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(ROOT, 'Resources/Textures')
PROTO = os.path.join(ROOT, 'Resources/Prototypes/_DarkestHour/Uprising/civ_clothing.yml')
FTL_RU = os.path.join(ROOT, 'Resources/Locale/ru-RU/ss14-ru/prototypes/_DarkestHour/Uprising/civ_clothing.ftl')
FTL_EN = os.path.join(ROOT, 'Resources/Locale/en-US/_DarkestHour/uprising_civ_clothing.ftl')
FTL_RU_UI = os.path.join(ROOT, 'Resources/Locale/ru-RU/_DarkestHour/uprising_civ_clothing.ftl')
C = '_RMC14/Objects/Clothing/'

# слот: (родитель, состояние на теле)
SLOTS = {
    'jumpsuit': ('ClothingUniformBase', 'equipped-INNERCLOTHING'),
    'outerClothing': ('ClothingOuterStorageBase', 'equipped-OUTERCLOTHING'),
    'head': ('ClothingHeadBase', 'equipped-HELMET'),
    'shoes': ('ClothingShoesBase', 'equipped-FEET'),
    'back': ('ClothingBackpackSatchel', 'equipped-BACKPACK'),
}
PREFIX = {'jumpsuit': 'Uniform', 'outerClothing': 'Outer', 'head': 'Head', 'shoes': 'Shoes', 'back': 'Back'}

# ключ, слот, rsi, наборы (casual/formal/work), en, ru
ITEMS = [
    # --- повседневное
    ('BlueJumpsuit', 'jumpsuit', 'Uniforms/Civilian/blue_jumpsuit', 'casual', 'blue jumpsuit', 'синий комбинезон'),
    ('BrownJumpsuit', 'jumpsuit', 'Uniforms/Civilian/brown_jumpsuit', 'casual', 'brown jumpsuit', 'коричневый комбинезон'),
    ('GreyJumpsuit', 'jumpsuit', 'Uniforms/Civilian/grey_jumpsuit', 'casual', 'grey jumpsuit', 'серый комбинезон'),
    ('LightBrownJumpsuit', 'jumpsuit', 'Uniforms/Civilian/lightbrown_jumpsuit', 'casual', 'light brown jumpsuit', 'светло-коричневый комбинезон'),
    ('YellowJumpsuit', 'jumpsuit', 'Uniforms/Civilian/yellow_jumpsuit', 'casual', 'yellow jumpsuit', 'жёлтый комбинезон'),
    ('WhiteJumpsuit', 'jumpsuit', 'Uniforms/Civilian/white_jumpsuit_alt', 'casual', 'white jumpsuit', 'белый комбинезон'),
    ('Checkered', 'jumpsuit', 'Uniforms/Civilian/checkered', 'casual', 'checkered shirt and jeans', 'клетчатая рубашка и джинсы'),
    ('DailyGrind', 'jumpsuit', 'Uniforms/Civilian/daily_grind', 'casual', 'everyday clothes', 'будничная одежда'),
    ('BlueUtility', 'jumpsuit', 'Uniforms/Civilian/blue_utility', 'casual work', 'blue utility clothes', 'синяя рабочая одежда'),
    ('BrownUtility', 'jumpsuit', 'Uniforms/Civilian/brown_utility', 'casual work', 'brown utility clothes', 'коричневая рабочая одежда'),
    ('GrayUtility', 'jumpsuit', 'Uniforms/Civilian/gray_utility', 'casual work', 'grey utility clothes', 'серая рабочая одежда'),
    ('GreenUtility', 'jumpsuit', 'Uniforms/Civilian/green_utility', 'casual work', 'green utility clothes', 'зелёная рабочая одежда'),
    ('RedUtility', 'jumpsuit', 'Uniforms/Civilian/red_utility', 'casual work', 'red utility clothes', 'красная рабочая одежда'),
    ('YellowUtility', 'jumpsuit', 'Uniforms/Civilian/yellow_utility', 'casual work', 'yellow utility clothes', 'жёлтая рабочая одежда'),
    ('KhakiWorkwear', 'jumpsuit', 'Uniforms/Civilian/khaki_workwear', 'casual work', 'khaki workwear', 'спецовка цвета хаки'),
    ('Frontier', 'jumpsuit', 'Uniforms/Survivor/frontier', 'casual', 'frontier clothes', 'одежда переселенца'),
    ('Jumper', 'jumpsuit', 'Uniforms/Survivor/kutjevo_jumper', 'casual work', 'work jumper', 'рабочий джемпер'),
    ('Shorts', 'jumpsuit', 'Uniforms/Survivor/varadero_shorts', 'casual', 'shirt and shorts', 'рубашка и шорты'),
    ('BlueShirtTanPants', 'jumpsuit', 'Uniforms/Freelancer/blueshirt_tanpants', 'casual', 'blue shirt and tan trousers', 'синяя рубашка и бежевые брюки'),
    ('OliveShirtBlackPants', 'jumpsuit', 'Uniforms/Freelancer/oliveshirt_blackpants', 'casual', 'olive shirt and black trousers', 'оливковая рубашка и чёрные брюки'),
    ('WhiteShirtGreenPants', 'jumpsuit', 'Uniforms/Freelancer/whiteshirt_greenpants', 'casual', 'white shirt and green trousers', 'белая рубашка и зелёные брюки'),
    ('TShirtGray', 'jumpsuit', 'Uniforms/T-shirt/gray', 'casual', 'grey t-shirt and jeans', 'серая футболка и джинсы'),
    ('TShirtRed', 'jumpsuit', 'Uniforms/T-shirt/red', 'casual', 'red t-shirt and jeans', 'красная футболка и джинсы'),
    ('TShirtWhite', 'jumpsuit', 'Uniforms/T-shirt/white', 'casual', 'white t-shirt and jeans', 'белая футболка и джинсы'),
    ('CargoShorts', 'jumpsuit', 'Uniforms/Liaison/cargo_shorts', 'casual', 'cargo shorts', 'шорты карго'),
    ('Outing', 'jumpsuit', 'Uniforms/Liaison/liaison_outing', 'casual formal', 'casual shirt', 'рубашка навыпуск'),
    ('OutingRed', 'jumpsuit', 'Uniforms/Liaison/liaison_outing_red', 'casual formal', 'red casual shirt', 'красная рубашка навыпуск'),
    ('GreenWorkwear', 'jumpsuit', 'Uniforms/Liaison/green_workwear', 'casual work', 'green workwear', 'зелёная спецовка'),
    ('GreyWorkwear', 'jumpsuit', 'Uniforms/Liaison/grey_workwear', 'casual work', 'grey workwear', 'серая спецовка'),
    ('PinkWorkwear', 'jumpsuit', 'Uniforms/Liaison/pink_workwear', 'casual', 'pink workwear', 'розовая спецовка'),
    ('Stowaway', 'jumpsuit', 'Uniforms/Liaison/stowaway', 'casual', 'worn-out clothes', 'поношенная одежда'),
    # --- деловое
    ('BlackPants', 'jumpsuit', 'Uniforms/Liaison/black_pants_uniform', 'formal', 'shirt and black trousers', 'рубашка и чёрные брюки'),
    ('BlackSuit', 'jumpsuit', 'Uniforms/Liaison/black_tailored', 'formal', 'black suit', 'чёрный костюм'),
    ('BurgundySuit', 'jumpsuit', 'Uniforms/Liaison/burgundy_tailored', 'formal', 'burgundy suit', 'бордовый костюм'),
    ('CheckeredSuit', 'jumpsuit', 'Uniforms/Liaison/checkered_tailored', 'formal', 'checkered suit', 'клетчатый костюм'),
    ('NavySuit', 'jumpsuit', 'Uniforms/Liaison/navy_tailored', 'formal', 'navy suit', 'тёмно-синий костюм'),
    ('ColonyAdmin', 'jumpsuit', 'Uniforms/Liaison/colony_admin', 'formal', 'administrator\'s uniform', 'форма администратора'),
    ('BlueBlazer', 'jumpsuit', 'Uniforms/Liaison/liaison_blue_blazer', 'formal', 'blue blazer', 'синий блейзер'),
    ('Regular', 'jumpsuit', 'Uniforms/Liaison/liaison_regular', 'formal', 'shirt and tie', 'рубашка с галстуком'),
    ('Formal', 'jumpsuit', 'Uniforms/Liaison/liaison_formal', 'formal', 'formal suit', 'парадный костюм'),
    ('Manager', 'jumpsuit', 'Uniforms/Liaison/managers_uniform', 'formal', 'manager\'s uniform', 'форма управляющего'),
    ('Legal', 'jumpsuit', 'Uniforms/Liaison/legal_uniform', 'formal', 'lawyer\'s suit', 'костюм юриста'),
    ('DetectiveBrown', 'jumpsuit', 'Uniforms/Liaison/detective_brown', 'formal casual', 'brown slacks and waistcoat', 'коричневые брюки и жилет'),
    ('DetectiveGrey', 'jumpsuit', 'Uniforms/Liaison/detective_grey', 'formal casual', 'grey slacks and waistcoat', 'серые брюки и жилет'),
    ('Skirt', 'jumpsuit', 'Uniforms/Liaison/liaison_skirt', 'formal', 'blouse and skirt', 'блузка и юбка'),
    ('ExpensiveSuit', 'jumpsuit', 'Uniforms/Liaison/expensive_suit', 'formal', 'expensive suit', 'дорогой костюм'),
    # --- рабочее
    ('EngineerWorkerAlt', 'jumpsuit', 'Uniforms/Survivor/engineer_worker_alt', 'work', 'technician\'s uniform', 'форма техника'),
    ('Sanitation', 'jumpsuit', 'Uniforms/Civilian/sanitation', 'work', 'sanitation coveralls', 'комбинезон коммунальной службы'),
    ('Mining', 'jumpsuit', 'Uniforms/Civilian/tartarus_mining', 'work', 'miner\'s coveralls', 'шахтёрский комбинезон'),
    # --- верхняя одежда
    ('BomberBlack', 'outerClothing', 'OuterClothing/Coats/bomber_black', 'casual', 'black bomber jacket', 'чёрный бомбер'),
    ('BomberGrey', 'outerClothing', 'OuterClothing/Coats/bomber_grey', 'casual', 'grey bomber jacket', 'серый бомбер'),
    ('BomberKhaki', 'outerClothing', 'OuterClothing/Coats/bomber_khaki', 'casual', 'khaki bomber jacket', 'бомбер цвета хаки'),
    ('BomberRed', 'outerClothing', 'OuterClothing/Coats/bomber_red', 'casual', 'red bomber jacket', 'красный бомбер'),
    ('BrownVest', 'outerClothing', 'OuterClothing/Coats/brown_vest', 'casual work', 'brown vest', 'коричневая жилетка'),
    ('GreyVest', 'outerClothing', 'OuterClothing/Coats/grey_vest', 'casual work', 'grey vest', 'серая жилетка'),
    ('TanVest', 'outerClothing', 'OuterClothing/Coats/tan_vest', 'casual work', 'tan vest', 'бежевая жилетка'),
    ('CoatBlue', 'outerClothing', 'OuterClothing/Coats/hobocoat_blue', 'casual', 'blue coat', 'синее пальто'),
    ('CoatBrown', 'outerClothing', 'OuterClothing/Coats/hobocoat_brown', 'casual', 'brown coat', 'коричневое пальто'),
    ('CoatDarkBlue', 'outerClothing', 'OuterClothing/Coats/hobocoat_dark_blue', 'casual', 'dark blue coat', 'тёмно-синее пальто'),
    ('CoatGreen', 'outerClothing', 'OuterClothing/Coats/hobocoat_green', 'casual', 'green coat', 'зелёное пальто'),
    ('CoatRed', 'outerClothing', 'OuterClothing/Coats/hobocoat_red', 'casual', 'red coat', 'красное пальто'),
    ('LongCoat', 'outerClothing', 'OuterClothing/Coats/long_coat', 'casual formal', 'long coat', 'длинный плащ'),
    ('FormalCoat', 'outerClothing', 'OuterClothing/Coats/coat_formal', 'formal', 'formal coat', 'строгое пальто'),
    ('Waistcoat', 'outerClothing', 'OuterClothing/Coats/waistcoat', 'formal', 'waistcoat', 'жилет'),
    ('TrenchCoat', 'outerClothing', 'OuterClothing/Coats/d_coat', 'formal casual', 'trench coat', 'тренчкот'),
    ('Windbreaker', 'outerClothing', 'OuterClothing/Coats/Windbreakers/windbreaker', 'casual work', 'windbreaker', 'ветровка'),
    ('WindbreakerBrown', 'outerClothing', 'OuterClothing/Coats/Windbreakers/windbreaker_brown', 'casual work', 'brown windbreaker', 'коричневая ветровка'),
    ('WindbreakerGrey', 'outerClothing', 'OuterClothing/Coats/Windbreakers/windbreaker_grey', 'casual work', 'grey windbreaker', 'серая ветровка'),
    ('WindbreakerExpedition', 'outerClothing', 'OuterClothing/Coats/Windbreakers/windbreaker_expedition', 'casual work', 'expedition windbreaker', 'экспедиционная ветровка'),
    # --- головные уборы
    ('TruckerBlue', 'head', 'Head/Hats/blue_trucker_cap', 'casual work', 'blue trucker cap', 'синяя кепка-тракер'),
    ('TruckerRed', 'head', 'Head/Hats/red_trucker_cap', 'casual work', 'red trucker cap', 'красная кепка-тракер'),
    ('Bucket', 'head', 'Head/Hats/bucket', 'casual', 'bucket hat', 'панама'),
    ('CapOld', 'head', 'Head/Hats/cargo_cap_old', 'casual work', 'old cap', 'старая кепка'),
    ('Flapcap', 'head', 'Head/Hats/flapcap/classic', 'casual work', 'flap cap', 'кепи с ушами'),
    ('SoftcapBlue', 'head', 'Head/Hats/softcap/blue_cap', 'casual', 'blue baseball cap', 'синяя бейсболка'),
    ('SoftcapYellow', 'head', 'Head/Hats/softcap/yellow_cap', 'casual', 'yellow baseball cap', 'жёлтая бейсболка'),
    ('Ushanka', 'head', 'Head/Hats/ushanka', 'casual', 'ushanka', 'ушанка'),
    ('Durag', 'head', 'Head/Hats/durag', 'casual', 'durag', 'дюраг'),
    ('DuragBlack', 'head', 'Head/Hats/durag_black', 'casual', 'black durag', 'чёрный дюраг'),
    ('FedoraBrown', 'head', 'Head/Hats/Fedora/brown', 'formal casual', 'brown fedora', 'коричневая федора'),
    ('FedoraGrey', 'head', 'Head/Hats/Fedora/grey', 'formal casual', 'grey fedora', 'серая федора'),
    ('FedoraTan', 'head', 'Head/Hats/Fedora/tan', 'formal casual', 'tan fedora', 'бежевая федора'),
    ('Beanie', 'head', 'Head/Beanies/beanie', 'casual work', 'beanie', 'вязаная шапка'),
    ('BeanieGray', 'head', 'Head/Beanies/gray', 'casual work', 'grey beanie', 'серая вязаная шапка'),
    ('BeanieGreen', 'head', 'Head/Beanies/green', 'casual work', 'green beanie', 'зелёная вязаная шапка'),
    ('BeanieTan', 'head', 'Head/Beanies/tan', 'casual work', 'tan beanie', 'бежевая вязаная шапка'),
    ('BoonieFisher', 'head', 'Head/Hats/boonie_fisher', 'casual', 'fisherman\'s hat', 'рыбацкая шляпа'),
    ('CowboyDark', 'head', 'Head/Hats/cowboy/dark_cowboy', 'casual', 'dark cowboy hat', 'тёмная ковбойская шляпа'),
    ('CowboyLight', 'head', 'Head/Hats/cowboy/light_cowboy', 'casual', 'light cowboy hat', 'светлая ковбойская шляпа'),
    # --- обувь
    ('SneakersBlue', 'shoes', 'Shoes/Sneakers/blue', 'casual', 'blue sneakers', 'синие кроссовки'),
    ('SneakersBrown', 'shoes', 'Shoes/Sneakers/brown', 'casual', 'brown sneakers', 'коричневые кроссовки'),
    ('SneakersGreen', 'shoes', 'Shoes/Sneakers/green', 'casual', 'green sneakers', 'зелёные кроссовки'),
    ('SneakersRed', 'shoes', 'Shoes/Sneakers/red', 'casual', 'red sneakers', 'красные кроссовки'),
    ('SneakersWhite', 'shoes', 'Shoes/Sneakers/white', 'casual', 'white sneakers', 'белые кроссовки'),
    ('SneakersYellow', 'shoes', 'Shoes/Sneakers/yellow', 'casual', 'yellow sneakers', 'жёлтые кроссовки'),
    ('LaceupBrown', 'shoes', 'Shoes/laceup_brown', 'formal', 'brown oxfords', 'коричневые оксфорды'),
    ('FancyLeather', 'shoes', 'Shoes/fancy_leather', 'formal', 'fancy leather shoes', 'лакированные туфли'),
    ('BootsGrey', 'shoes', 'Shoes/Boots/grey', 'work casual', 'grey boots', 'серые ботинки'),
    ('BootsBlack', 'shoes', 'Shoes/Boots/black', 'work casual', 'black boots', 'чёрные ботинки'),
    # --- сумки
    ('SatchelBlack', 'back', 'Back/Satchels/Civilians/black', 'casual formal', 'black satchel', 'чёрная сумка'),
    ('SatchelBlue', 'back', 'Back/Satchels/Civilians/blue', 'casual', 'blue satchel', 'синяя сумка'),
    ('SatchelClassic', 'back', 'Back/Satchels/Civilians/classic', 'casual formal', 'leather satchel', 'кожаная сумка'),
    ('Backpack', 'back', 'Back/Backpacks/Civilians/standard', 'casual work', 'backpack', 'рюкзак'),
]

# уже существующие вещи (Uprising/clothing_city_rmc.yml и ванильные), которые входят в наборы
EXTRA = {
    'jumpsuit': {
        'casual': ['DHLAClothingUniformColonist'],
        'formal': ['DHLAClothingUniformSuitCharcoal', 'DHLAClothingUniformAdmin', 'DHLAClothingUniformOffice',
                   'DHLAClothingUniformSuspenders'],
        'work': ['DHLAClothingUniformOveralls', 'DHLAClothingUniformUtility', 'DHLAClothingUniformMetro'],
    },
    'outerClothing': {'casual': [], 'formal': ['ClothingOuterCoatGentle'], 'work': ['ClothingOuterVestHazard']},
    'head': {'casual': [], 'formal': [], 'work': ['DHLAClothingHeadCargoCap', 'ClothingHeadHatHardhatYellow',
                                                    'ClothingHeadHatHardhatOrange']},
    'shoes': {'casual': ['DHLAClothingShoesSneakers'], 'formal': ['DHLAClothingShoesLeather', 'DHLAClothingShoesLaceup'],
              'work': ['DHLAClothingShoesWorkBoots']},
    'back': {'casual': ['DHLAClothingBackpackSatchel'], 'formal': [], 'work': []},
}

# случайные заготовки: id, слот, набор, шанс «без вещи», en, ru
RANDOM = [
    ('DHLACivRandomUniformCasual', 'jumpsuit', 'casual', 0, 'random casual clothes', 'случайная повседневная одежда'),
    ('DHLACivRandomUniformFormal', 'jumpsuit', 'formal', 0, 'random business clothes', 'случайная деловая одежда'),
    ('DHLACivRandomUniformWork', 'jumpsuit', 'work', 0, 'random work clothes', 'случайная рабочая одежда'),
    ('DHLACivRandomOuterCasual', 'outerClothing', 'casual', 0.45, 'random jacket (or none)', 'случайная куртка (или без неё)'),
    ('DHLACivRandomOuterFormal', 'outerClothing', 'formal', 0.5, 'random coat (or none)', 'случайное пальто (или без него)'),
    ('DHLACivRandomHeadCasual', 'head', 'casual', 0.55, 'random hat (or none)', 'случайный головной убор (или без него)'),
    ('DHLACivRandomShoesCasual', 'shoes', 'casual', 0, 'random shoes', 'случайная обувь'),
    ('DHLACivRandomShoesFormal', 'shoes', 'formal', 0, 'random formal shoes', 'случайные туфли'),
    ('DHLACivRandomBackCasual', 'back', 'casual', 0, 'random bag', 'случайная сумка'),
]

# должность: {слот: вещь по умолчанию (None — слот необязателен)}, наборы для списка
JOBS = {
    'DHLAResident': ({'jumpsuit': 'DHLACivRandomUniformCasual', 'outerClothing': 'DHLACivRandomOuterCasual',
                      'head': 'DHLACivRandomHeadCasual', 'shoes': 'DHLACivRandomShoesCasual',
                      'back': 'DHLACivRandomBackCasual'}, ['casual', 'work', 'formal']),
    'DHLAEntrepreneur': ({'jumpsuit': 'DHLAClothingUniformSuspenders', 'outerClothing': 'ClothingOuterApronBar',
                          'head': None, 'shoes': 'DHLAClothingShoesLaceup', 'back': 'DHLAClothingBackpackSatchel'},
                         ['casual', 'formal']),
    'DHLAOfficeWorker': ({'jumpsuit': 'DHLACivRandomUniformFormal', 'outerClothing': 'DHLACivRandomOuterFormal',
                          'head': None, 'shoes': 'DHLACivRandomShoesFormal', 'back': 'DHLAClothingBackpackSatchel'},
                         ['formal', 'casual']),
    'DHLADistrictStaff': ({'jumpsuit': 'DHLAClothingUniformAdmin', 'outerClothing': None, 'head': None,
                           'shoes': 'DHLAClothingShoesLeather', 'back': 'DHLAClothingBackpackSatchel'}, ['formal']),
    'DHLADistrictHead': ({'jumpsuit': 'DHLAClothingUniformSuitCharcoal', 'outerClothing': 'ClothingOuterCoatGentle',
                          'head': None, 'shoes': 'DHLAClothingShoesLeather', 'back': 'DHLAClothingBackpackSatchel'},
                         ['formal']),
    'DHLAFactoryWorker': ({'jumpsuit': 'DHLAClothingUniformOveralls', 'outerClothing': None,
                           'head': 'ClothingHeadHatHardhatYellow', 'shoes': 'DHLAClothingShoesWorkBoots',
                           'back': 'DHLAClothingBackpackSatchel'}, ['work', 'casual']),
    'DHLAUtilityWorker': ({'jumpsuit': 'DHLAClothingUniformUtility', 'outerClothing': 'ClothingOuterVestHazard',
                           'head': 'ClothingHeadHatHardhatOrange', 'shoes': 'DHLAClothingShoesWorkBoots',
                           'back': 'DHLAClothingBackpackSatchel'}, ['work', 'casual']),
    'DHLAMetroWorker': ({'jumpsuit': 'DHLAClothingUniformMetro', 'outerClothing': None,
                         'head': 'DHLAClothingHeadCargoCap', 'shoes': 'DHLAClothingShoesWorkBoots',
                         'back': 'DHLAClothingBackpackSatchel'}, ['work', 'casual']),
}
GROUP_NAME = {'jumpsuit': 'dh-loadout-group-la-jumpsuit', 'outerClothing': 'dh-loadout-group-la-outer',
              'head': 'dh-loadout-group-la-head', 'shoes': 'dh-loadout-group-la-shoes',
              'back': 'dh-loadout-group-la-back'}
GROUP_TEXT = {'jumpsuit': ('Clothes', 'Одежда'), 'outerClothing': ('Outerwear', 'Верхняя одежда'),
              'head': ('Headwear', 'Головной убор'), 'shoes': ('Shoes', 'Обувь'), 'back': ('Bag', 'Сумка')}
EXTRA_GROUPS = ['Glasses', 'Trinkets']


def item_id(key, slot):
    return 'DHLACiv%s%s' % (PREFIX[slot], key)


def check(rmc_tex):
    for key, slot, rsi, _, _, _ in ITEMS:
        meta = json.load(open(os.path.join(rmc_tex, C + rsi + '.rsi', 'meta.json'), encoding='utf-8-sig'))
        names = {s['name'] for s in meta['states']}
        need = {'icon', SLOTS[slot][1]}
        if not need <= names:
            raise SystemExit('%s: нет состояний %s' % (rsi, need - names))


def pool(slot, sets):
    res = []
    for s in sets:
        res += [item_id(k, sl) for k, sl, _, tags, _, _ in ITEMS if sl == slot and s in tags.split()]
        res += EXTRA[slot].get(s, [])
    out = []
    for r in res:
        if r not in out:
            out.append(r)
    return out


def loadout_id(proto):
    return 'DHLALoadout' + (proto[4:] if proto.startswith('DHLA') else proto)


def main(rmc):
    rmc_tex = os.path.join(rmc, 'Resources/Textures')
    check(rmc_tex)
    for _, _, rsi, _, _, _ in ITEMS:
        dst = os.path.join(TEX, C + rsi + '.rsi')
        if os.path.isdir(dst):
            shutil.rmtree(dst)
        shutil.copytree(os.path.join(rmc_tex, C + rsi + '.rsi'), dst)

    y = ['# DarkestHour: сгенерировано Tools/_DarkestHour/uprising/gen_civ_clothing.py — правьте генератор.',
         '# Гражданская одежда Лост-Энджелса (RMC-14, CC-BY-SA-3.0), случайные заготовки и лоадауты мирных должностей.', '']
    ru = ['# DarkestHour: сгенерировано Tools/_DarkestHour/uprising/gen_civ_clothing.py.']
    for key, slot, rsi, _, en, ru_name in ITEMS:
        pid = item_id(key, slot)
        parent = SLOTS[slot][0]
        y += ['- type: entity', '  parent: %s' % parent, '  id: %s' % pid, '  name: %s' % en,
              '  description: "Everyday clothing of Lost Angels."', '  components:', '  - type: Sprite',
              '    sprite: %s' % (C + rsi + '.rsi')]
        if slot == 'back':
            y += ['    state: icon']
        y += ['  - type: Clothing', '    sprite: %s' % (C + rsi + '.rsi'), '']
        ru += ['ent-%s = %s' % (pid, ru_name), '    .desc = Повседневная одежда Лост-Энджелса.']

    for rid, slot, sets, empty, en, ru_name in RANDOM:
        choices = pool(slot, [sets])
        # заготовка выглядит как первая вещь набора, пока сервер не выбрал
        first = next(k for k in ITEMS if k[1] == slot and sets in k[3].split())
        sprite = C + first[2] + '.rsi'
        y += ['- type: entity', '  parent: %s' % SLOTS[slot][0], '  id: %s' % rid, '  name: %s' % en,
              '  description: "Turns into a random piece of clothing when you arrive in the city."',
              '  suffix: random', '  components:', '  - type: Sprite', '    sprite: %s' % sprite, '    state: icon',
              '  - type: Clothing', '    sprite: %s' % sprite, '  - type: DHRandomOutfit']
        if empty:
            y += ['    emptyChance: %s' % empty]
        y += ['    choices:'] + ['    - %s' % c for c in choices if not c.startswith('ClothingHeadHatHardhat')
                                  and c not in ('ClothingOuterVestHazard', 'ClothingOuterCoatGentle')] + ['']
        ru += ['ent-%s = %s' % (rid, ru_name), '    .desc = При появлении в городе становится случайной вещью.',
               '    .suffix = случайное']

    # лоадауты: по одному на вещь
    every = set()
    for job, (defaults, sets) in JOBS.items():
        for slot in SLOTS:
            every.update(pool(slot, sets))
            if defaults.get(slot):
                every.add(defaults[slot])
        every.update(r[0] for r in RANDOM)
    for proto in sorted(every):
        slot = next((s for k, s, *_ in ITEMS if item_id(k, s) == proto), None)
        if slot is None:
            slot = next((r[1] for r in RANDOM if r[0] == proto), None)
        if slot is None:
            slot = next(s for s, sets in EXTRA.items() if any(proto in v for v in sets.values())) \
                if any(proto in v for sets in EXTRA.values() for v in sets.values()) else \
                next(s for job in JOBS.values() for s, d in job[0].items() if d == proto)
        y += ['- type: loadout', '  id: %s' % loadout_id(proto), '  equipment:', '    %s: %s' % (slot, proto), '']

    for job, (defaults, sets) in JOBS.items():
        groups = []
        for slot in SLOTS:
            default = defaults.get(slot)
            items = []
            if default:
                items.append(default)
            items += [r[0] for r in RANDOM if r[1] == slot]
            items += pool(slot, sets)
            seen = []
            for i in items:
                if i not in seen:
                    seen.append(i)
            gid = 'DHLA%s%s' % (job[4:], PREFIX[slot])
            groups.append(gid)
            y += ['- type: loadoutGroup', '  id: %s' % gid, '  name: %s' % GROUP_NAME[slot]]
            if not default:
                y += ['  minLimit: 0']
            y += ['  loadouts:'] + ['  - %s' % loadout_id(i) for i in seen] + ['']
        y += ['- type: roleLoadout', '  id: Job%s' % job, '  groups:'] + ['  - %s' % g for g in groups + EXTRA_GROUPS] + ['']

    open(PROTO, 'w', encoding='utf-8').write('\n'.join(y).rstrip('\n') + '\n')
    os.makedirs(os.path.dirname(FTL_RU), exist_ok=True)
    open(FTL_RU, 'w', encoding='utf-8').write('\n'.join(ru) + '\n')
    open(FTL_EN, 'w', encoding='utf-8').write(''.join('%s = %s\n' % (GROUP_NAME[s], GROUP_TEXT[s][0]) for s in SLOTS))
    open(FTL_RU_UI, 'w', encoding='utf-8').write(''.join('%s = %s\n' % (GROUP_NAME[s], GROUP_TEXT[s][1]) for s in SLOTS))
    print('ok', len(ITEMS), 'items,', len(every), 'loadouts')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '/home/claude/rmc')
