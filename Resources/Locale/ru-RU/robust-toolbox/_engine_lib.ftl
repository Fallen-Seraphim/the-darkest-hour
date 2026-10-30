zzzz-conjugate-basic = { GENDER($ent) ->
    [epicene] { $first }
    *[other] { $second }
}

zzzz-conjugate-be = { GENDER($ent) ->
    [epicene] are
    *[other] is
}

zzzz-conjugate-have = { GENDER($ent) ->
    [epicene] имеют
    *[other] имеет
}

zzzz-dat-object = { GENDER($ent) ->
    [male] ему
    [female] ей
    [epicene] им
    *[neuter] ему
}

zzzz-genitive = { GENDER($ent) ->
    [male] него
    [female] неё
    [epicene] них
    *[neuter] него
}

zzzz-object-pronoun = { GENDER($ent) ->
    [male] его
    [female] её
    [epicene] их
    *[neuter] его
}

zzzz-possessive-adjective = { GENDER($ent) ->
    [male] его
    [female] её
    [epicene] их
    *[neuter] его
}

zzzz-possessive-pronoun = { GENDER($ent) ->
    [male] его
    [female] её
    [epicene] их
    *[neuter] его
}

zzzz-reflexive-pronoun = { GENDER($ent) ->
    [male] сам
    [female] сама
    [epicene] сами
    *[neuter] само
}

zzzz-subject-pronoun = { GENDER($ent) ->
    [male] он
    [female] она
    [epicene] они
    *[neuter] оно
}

zzzz-the = { PROPER($ent) ->
    *[false] { $ent }
    [true] { $ent }
}
