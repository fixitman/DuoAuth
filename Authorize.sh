#!/bin/bash

#THIS SCRIPT IS USED TO RUN DUO 2FA
#$1 SHOULD BE THE USER'S START MENU


if ! test -e ./bypass || test `find ./bypass -mtime +1` ; then

        /appl/fp/dclerk glauth -s0 -de -z auth -xa && /appl/fp/runmenu $1
else
        /appl/fp/runmenu $1
fi

