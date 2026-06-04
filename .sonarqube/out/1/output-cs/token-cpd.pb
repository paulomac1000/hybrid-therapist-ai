²
^/home/pablo/Projects/hybrid-therapist/src/HybridTherapist.Security/Privacy/PrivacySanitizer.cs
	namespace 	
HybridTherapist
 
. 
Security "
." #
Privacy# *
;* +
public		 
sealed		 
partial		 
class		 
PrivacySanitizer		 ,
{

 
[ 
GeneratedRegex 
( 
$str Z
,Z [
RegexOptions\ h
.h i
Nonei m
,m n%
matchTimeoutMilliseconds	o ‡
:
‡ ˆ
$num
‰ Œ
)
Œ 
]
 Ž
private 
static 
partial 
Regex  
FullNamePattern! 0
(0 1
)1 2
;2 3
[ 
GeneratedRegex 
( 
$str K
,K L
RegexOptionsM Y
.Y Z
NoneZ ^
,^ _$
matchTimeoutMilliseconds` x
:x y
$numz }
)} ~
]~ 
private 
static 
partial 
Regex  
EmailPattern! -
(- .
). /
;/ 0
[ 
GeneratedRegex 
( 
$str B
,B C
RegexOptionsD P
.P Q
NoneQ U
,U V$
matchTimeoutMillisecondsW o
:o p
$numq t
)t u
]u v
private 
static 
partial 
Regex  
PhonePattern! -
(- .
). /
;/ 0
[ 
GeneratedRegex 
( 
$str !
,! "
RegexOptions# /
./ 0
None0 4
,4 5$
matchTimeoutMilliseconds6 N
:N O
$numP S
)S T
]T U
private 
static 
partial 
Regex  
PeselPattern! -
(- .
). /
;/ 0
public 

string 
Sanitize 
( 
string !
input" '
,' (
string) /
level0 5
=6 7
$str8 ?
)? @
{ 
if 

( 
string 
. 
IsNullOrWhiteSpace %
(% &
input& +
)+ ,
), -
return 
input 
; 
string 
result 
= 
input 
; 
try 
{ 	
result   
=   
EmailPattern   !
(  ! "
)  " #
.  # $
Replace  $ +
(  + ,
result  , 2
,  2 3
$str  4 F
)  F G
;  G H
result!! 
=!! 
PhonePattern!! !
(!!! "
)!!" #
.!!# $
Replace!!$ +
(!!+ ,
result!!, 2
,!!2 3
$str!!4 F
)!!F G
;!!G H
result"" 
="" 
PeselPattern"" !
(""! "
)""" #
.""# $
Replace""$ +
(""+ ,
result"", 2
,""2 3
$str""4 F
)""F G
;""G H
if$$ 
($$ 
!$$ 
string$$ 
.$$ 
Equals$$ 
($$ 
level$$ $
,$$$ %
$str$$& -
,$$- .
StringComparison$$/ ?
.$$? @
OrdinalIgnoreCase$$@ Q
)$$Q R
)$$R S
result%% 
=%% 
FullNamePattern%% (
(%%( )
)%%) *
.%%* +
Replace%%+ 2
(%%2 3
result%%3 9
,%%9 :
$str%%; L
)%%L M
;%%M N
}&& 	
catch'' 
('' &
RegexMatchTimeoutException'' )
)'') *
{(( 	
}** 	
return,, 
result,, 
;,, 
}-- 
}.. ÷>
V/home/pablo/Projects/hybrid-therapist/src/HybridTherapist.Security/Gates/CrisisGate.cs
	namespace 	
HybridTherapist
 
. 
Security "
." #
Gates# (
;( )
public

 
sealed

 
partial

 
class

 

CrisisGate

 &
:

' (
ICrisisGate

) 4
{ 
private 
const 
string 
HardStopMessage (
=) *
$str c
+d e
$str i
+j k
$str V
;V W
[ 
GeneratedRegex 
( 
$str \
,\ ]
RegexOptions 
. 

IgnoreCase 
,  $
matchTimeoutMilliseconds! 9
:9 :
$num; >
)> ?
]? @
private 
static 
partial 
Regex  

HardStopPl! +
(+ ,
), -
;- .
[ 
GeneratedRegex 
( 
$str K
,K L
RegexOptions 
. 

IgnoreCase 
,  $
matchTimeoutMilliseconds! 9
:9 :
$num; >
)> ?
]? @
private 
static 
partial 
Regex  

HardStopEn! +
(+ ,
), -
;- .
[ 
GeneratedRegex 
( 
$str x
,x y
RegexOptions 
. 

IgnoreCase 
,  $
matchTimeoutMilliseconds! 9
:9 :
$num; >
)> ?
]? @
private   
static   
partial   
Regex    
HighSeverityPl  ! /
(  / 0
)  0 1
;  1 2
[## 
GeneratedRegex## 
(## 
$str	$$ º
,
$$º »
RegexOptions%% 
.%% 

IgnoreCase%% 
,%%  $
matchTimeoutMilliseconds%%! 9
:%%9 :
$num%%; >
)%%> ?
]%%? @
private&& 
static&& 
partial&& 
Regex&&  
MediumSeverityPl&&! 1
(&&1 2
)&&2 3
;&&3 4
[)) 
GeneratedRegex)) 
()) 
$str	** €
,
**€ 
RegexOptions++ 
.++ 

IgnoreCase++ 
,++  $
matchTimeoutMilliseconds++! 9
:++9 :
$num++; >
)++> ?
]++? @
private,, 
static,, 
partial,, 
Regex,,  
AnhedoniaPl,,! ,
(,,, -
),,- .
;,,. /
[// 
GeneratedRegex// 
(// 
$str	00 Š
,
00Š ‹
RegexOptions11 
.11 

IgnoreCase11 
,11  $
matchTimeoutMilliseconds11! 9
:119 :
$num11; >
)11> ?
]11? @
private22 
static22 
partial22 
Regex22  
SocialWithdrawalPl22! 3
(223 4
)224 5
;225 6
[55 
GeneratedRegex55 
(55 
$str	66 ¹
,
66¹ º
RegexOptions77 
.77 

IgnoreCase77 
,77  $
matchTimeoutMilliseconds77! 9
:779 :
$num77; >
)77> ?
]77? @
private88 
static88 
partial88 
Regex88  
PanicAnxietyPl88! /
(88/ 0
)880 1
;881 2
[;; 
GeneratedRegex;; 
(;; 
$str	<< ¥
,
<<¥ ¦
RegexOptions== 
.== 

IgnoreCase== 
,==  $
matchTimeoutMilliseconds==! 9
:==9 :
$num==; >
)==> ?
]==? @
private>> 
static>> 
partial>> 
Regex>>  
AngerPl>>! (
(>>( )
)>>) *
;>>* +
[AA 
GeneratedRegexAA 
(AA 
$str	BB ¤
,
BB¤ ¥
RegexOptionsCC 
.CC 

IgnoreCaseCC 
,CC  $
matchTimeoutMillisecondsCC! 9
:CC9 :
$numCC; >
)CC> ?
]CC? @
privateDD 
staticDD 
partialDD 
RegexDD  
CognitivePlDD! ,
(DD, -
)DD- .
;DD. /
[GG 
GeneratedRegexGG 
(GG 
$str	HH 
,
HH 
RegexOptionsII 
.II 

IgnoreCaseII 
,II  $
matchTimeoutMillisecondsII! 9
:II9 :
$numII; >
)II> ?
]II? @
privateJJ 
staticJJ 
partialJJ 
RegexJJ  
InsomniaExtendedPlJJ! 3
(JJ3 4
)JJ4 5
;JJ5 6
publicLL 

CrisisGateResultLL 
CheckLL !
(LL! "
stringLL" (
inputLL) .
)LL. /
{MM 
ifNN 

(NN 
stringNN 
.NN 
IsNullOrWhiteSpaceNN %
(NN% &
inputNN& +
)NN+ ,
)NN, -
returnOO 
CrisisGateResultOO #
.OO# $
SafeOO$ (
;OO( )
tryQQ 
{RR 	
ifSS 
(SS 

HardStopPlSS 
(SS 
)SS 
.SS 
IsMatchSS $
(SS$ %
inputSS% *
)SS* +
||SS, .

HardStopEnSS/ 9
(SS9 :
)SS: ;
.SS; <
IsMatchSS< C
(SSC D
inputSSD I
)SSI J
)SSJ K
returnTT 
CrisisGateResultTT '
.TT' (
HardStopTT( 0
(TT0 1
HardStopMessageTT1 @
)TT@ A
;TTA B
ifVV 
(VV 
HighSeverityPlVV 
(VV 
)VV  
.VV  !
IsMatchVV! (
(VV( )
inputVV) .
)VV. /
)VV/ 0
returnWW 
CrisisGateResultWW '
.WW' (

EscalationWW( 2
(WW2 3
$strWW3 9
)WW9 :
;WW: ;
ifYY 
(YY 
AnhedoniaPlYY 
(YY 
)YY 
.YY 
IsMatchYY %
(YY% &
inputYY& +
)YY+ ,
||YY- /
PanicAnxietyPlYY0 >
(YY> ?
)YY? @
.YY@ A
IsMatchYYA H
(YYH I
inputYYI N
)YYN O
)YYO P
returnZZ 
CrisisGateResultZZ '
.ZZ' (

EscalationZZ( 2
(ZZ2 3
$strZZ3 9
)ZZ9 :
;ZZ: ;
if\\ 
(\\ 
SocialWithdrawalPl\\ "
(\\" #
)\\# $
.\\$ %
IsMatch\\% ,
(\\, -
input\\- 2
)\\2 3
||\\4 6
AngerPl\\7 >
(\\> ?
)\\? @
.\\@ A
IsMatch\\A H
(\\H I
input\\I N
)\\N O
||\\P R
CognitivePl]] 
(]] 
)]] 
.]] 
IsMatch]] %
(]]% &
input]]& +
)]]+ ,
||]]- /
InsomniaExtendedPl]]0 B
(]]B C
)]]C D
.]]D E
IsMatch]]E L
(]]L M
input]]M R
)]]R S
)]]S T
return^^ 
CrisisGateResult^^ '
.^^' (

Escalation^^( 2
(^^2 3
$str^^3 =
)^^= >
;^^> ?
if`` 
(`` 
MediumSeverityPl``  
(``  !
)``! "
.``" #
IsMatch``# *
(``* +
input``+ 0
)``0 1
)``1 2
returnaa 
CrisisGateResultaa '
.aa' (

Escalationaa( 2
(aa2 3
$straa3 ;
)aa; <
;aa< =
}bb 	
catchcc 
(cc &
RegexMatchTimeoutExceptioncc )
)cc) *
{dd 	
}ff 	
returnhh 
CrisisGateResulthh 
.hh  
Safehh  $
;hh$ %
}ii 
}jj 