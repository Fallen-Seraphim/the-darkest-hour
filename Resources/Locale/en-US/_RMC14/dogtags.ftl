# Ported from RMC-14 (MIT): job/cm-id.ftl, foldable.ftl, rmc-memorial.ftl.
# DarkestHour: rmc-dogtags-* strings were rewritten for the port (the RMC originals were not carried over).

cm-access-dogtags-component-owner-name-job-title-text = dog tags{$jobSuffix}
cm-access-dogtags-component-owner-full-name-job-title-text = {$fullName}{$jobSuffix}

rmc-dogtag-verb-hide = Hide dog tags
rmc-dogtag-verb-show = Show dog tags

rmc-memorial-start = To the fallen:
rmc-memorial-add = You add {THE($tags)} to {THE($slab)}.

rmc-dogtags-read = The tags read: [bold]{$name}[/bold], {$assignment}. Blood type: {$bloodtype}.
rmc-dogtags-info-read-start = There {$tags ->
    [one] is one name
   *[other] are {$tags} names
} stamped on the information tags:
rmc-dogtags-info-read = {$number}. [bold]{$name}[/bold], {$assignment}. Blood type: {$bloodtype}.
rmc-dogtags-unknown = Unknown
rmc-dogtags-take = Take information tag
rmc-dogtags-still-alive = They are still alive. Leave their tags alone.
rmc-dogtags-already-taken = Someone has already taken {THE($target)}'s information tag.
rmc-dogtags-info-joined-name = information dog tags
rmc-dogtags-info-joined-desc = Information dog tags of several dead crewmembers, strung together.
rmc-dogtags-single-join = You join the two tags together.
rmc-dogtags-join = You join the tags together.
