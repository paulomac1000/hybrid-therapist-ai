ö,
e/home/pablo/Projects/hybrid-therapist/src/HybridTherapist.Infrastructure/Tracing/InMemoryTraceSink.cs
	namespace 	
HybridTherapist
 
. 
Infrastructure (
.( )
Tracing) 0
;0 1
public

 
sealed

 
class

 
InMemoryTraceSink

 %
:

& '

ITraceSink

( 2
{ 
private 
const 
int 
MaxEventsPerSession )
=* +
$num, /
;/ 0
private 
static 
readonly 
TimeSpan $
Ttl% (
=) *
TimeSpan+ 3
.3 4
	FromHours4 =
(= >
$num> ?
)? @
;@ A
private 
readonly  
ConcurrentDictionary )
<) *
string* 0
,0 1
List2 6
<6 7

TraceEvent7 A
>A B
>B C
_storeD J
=K L
newM P
(P Q
)Q R
;R S
public 

Task 
RecordAsync 
( 

TraceEvent &
evt' *
,* +
CancellationToken, =
ct> @
=A B
defaultC J
)J K
{ 
List 
< 

TraceEvent 
> 
events 
=  !
_store" (
.( )
GetOrAdd) 1
(1 2
evt2 5
.5 6
	SessionId6 ?
,? @
_A B
=>C E
[F G
]G H
)H I
;I J
lock 
( 
events 
) 
{ 	
events 
. 
Add 
( 
evt 
) 
; 
if 
( 
events 
. 
Count 
> 
MaxEventsPerSession 2
)2 3
events 
. 
RemoveRange "
(" #
$num# $
,$ %
events& ,
., -
Count- 2
-3 4
MaxEventsPerSession5 H
)H I
;I J
} 	
EvictExpired 
( 
) 
; 
return 
Task 
. 
CompletedTask !
;! "
} 
public   

Task   
<   
IReadOnlyList   
<   

TraceEvent   (
>  ( )
>  ) *
GetAsync  + 3
(  3 4
string  4 :
	sessionId  ; D
,  D E
CancellationToken  F W
ct  X Z
=  [ \
default  ] d
)  d e
{!! 
if"" 

("" 
!"" 
_store"" 
."" 
TryGetValue"" 
(""  
	sessionId""  )
,"") *
out""+ .
List""/ 3
<""3 4

TraceEvent""4 >
>""> ?
?""? @
events""A G
)""G H
)""H I
return## 
Task## 
.## 

FromResult## "
<##" #
IReadOnlyList### 0
<##0 1

TraceEvent##1 ;
>##; <
>##< =
(##= >
Array##> C
.##C D
Empty##D I
<##I J

TraceEvent##J T
>##T U
(##U V
)##V W
)##W X
;##X Y
lock%% 
(%% 
events%% 
)%% 
{&& 	
return'' 
Task'' 
.'' 

FromResult'' "
<''" #
IReadOnlyList''# 0
<''0 1

TraceEvent''1 ;
>''; <
>''< =
(''= >
events''> D
.''D E
ToArray''E L
(''L M
)''M N
)''N O
;''O P
}(( 	
})) 
public++ 

Task++ 

ClearAsync++ 
(++ 
string++ !
	sessionId++" +
,+++ ,
CancellationToken++- >
ct++? A
=++B C
default++D K
)++K L
{,, 
_store-- 
.-- 
	TryRemove-- 
(-- 
	sessionId-- "
,--" #
out--$ '
_--( )
)--) *
;--* +
return.. 
Task.. 
... 
CompletedTask.. !
;..! "
}// 
private11 
void11 
EvictExpired11 
(11 
)11 
{22 
DateTimeOffset33 
cutoff33 
=33 
DateTimeOffset33  .
.33. /
UtcNow33/ 5
-336 7
Ttl338 ;
;33; <
foreach44 
(44 
KeyValuePair44 
<44 
string44 $
,44$ %
List44& *
<44* +

TraceEvent44+ 5
>445 6
>446 7
kvp448 ;
in44< >
_store44? E
)44E F
{55 	
lock66 
(66 
kvp66 
.66 
Value66 
)66 
{77 
if88 
(88 
kvp88 
.88 
Value88 
.88 
Count88 #
>88$ %
$num88& '
&&88( *
kvp88+ .
.88. /
Value88/ 4
[884 5
^885 6
$num886 7
]887 8
.888 9
	Timestamp889 B
<88C D
cutoff88E K
)88K L
_store99 
.99 
	TryRemove99 $
(99$ %
kvp99% (
.99( )
Key99) ,
,99, -
out99. 1
_992 3
)993 4
;994 5
}:: 
};; 	
}<< 
}== ²
p/home/pablo/Projects/hybrid-therapist/src/HybridTherapist.Infrastructure/State/InMemoryTherapyStateRepository.cs
	namespace 	
HybridTherapist
 
. 
Infrastructure (
.( )
State) .
;. /
public 
sealed 
class *
InMemoryTherapyStateRepository 2
:3 4/
#ITherapyConversationStateRepository5 X
{ 
private 
readonly  
ConcurrentDictionary )
<) *
string* 0
,0 1$
TherapyConversationState2 J
>J K
_storeL R
=S T
newU X
(X Y
)Y Z
;Z [
public 

Task 
< $
TherapyConversationState (
>( )
GetAsync* 2
(2 3
string3 9
	sessionId: C
,C D
CancellationTokenE V
ctW Y
=Z [
default\ c
)c d
{ $
TherapyConversationState  
state! &
=' (
_store) /
./ 0
GetOrAdd0 8
(8 9
	sessionId9 B
,B C
idD F
=>G I
newJ M$
TherapyConversationStateN f
{ 	
	SessionId 
= 
id 
, 
CurrentPhase 
= 
$str !
,! "
Topics 
= 
[ 
] 
, 
History 
= 
[ 
] 
, 
} 	
)	 

;
 
return 
Task 
. 

FromResult 
( 
state $
)$ %
;% &
} 
public 

Task 
	SaveAsync 
( $
TherapyConversationState 2
state3 8
,8 9
CancellationToken: K
ctL N
=O P
defaultQ X
)X Y
{ 
_store 
[ 
state 
. 
	SessionId 
] 
=  !
state" '
;' (
return 
Task 
. 
CompletedTask !
;! "
}   
}!! ÉK
b/home/pablo/Projects/hybrid-therapist/src/HybridTherapist.Infrastructure/Adapters/OllamaAdapter.cs
	namespace 	
HybridTherapist
 
. 
Infrastructure (
.( )
Adapters) 1
;1 2
public 
sealed 
class 
LlmResponse 
{ 
public		 

bool		 
Ok		 
{		 
get		 
;		 
init		 
;		 
}		  !
public

 

string

 
Text

 
{

 
get

 
;

 
init

 "
;

" #
}

$ %
=

& '
string

( .
.

. /
Empty

/ 4
;

4 5
public 

string 
? 
Error 
{ 
get 
; 
init  $
;$ %
}& '
public 

string 
? 
ModelId 
{ 
get  
;  !
init" &
;& '
}( )
} 
public 
sealed 
class 
OllamaAdapter !
:" #
IOllamaAdapter$ 2
{ 
private 
readonly 
IHttpClientFactory '
_factory( 0
;0 1
public 

OllamaAdapter 
( 
IHttpClientFactory +
factory, 3
)3 4
=>5 7
_factory8 @
=A B
factoryC J
;J K
public 

Task 
< 
LlmResponse 
> 
GenerateAsync *
(* +
string 
prompt 
, 
string 
? 
systemPrompt +
,+ ,
int- 0
	maxTokens1 :
,: ;
float< A
temperatureB M
,M N
string 
modelId 
, 
CancellationToken )
ct* ,
=- .
default/ 6
)6 7
{ 
object 
[ 
] 
messages 
= 
string "
." #
IsNullOrWhiteSpace# 5
(5 6
systemPrompt6 B
)B C
?   
[   
new   
{   
role   
=   
$str   "
,  " #
content  $ +
=  , -
prompt  . 4
}  5 6
]  6 7
:!! 
[!! 
new!! 
{!! 
role!! 
=!! 
$str!! $
,!!$ %
content!!& -
=!!. /
systemPrompt!!0 <
}!!= >
,!!> ?
new!!@ C
{!!D E
role!!F J
=!!K L
$str!!M S
,!!S T
content!!U \
=!!] ^
prompt!!_ e
}!!f g
]!!g h
;!!h i
return## 
	SendAsync## 
(## 
messages## !
,##! "
	maxTokens### ,
,##, -
temperature##. 9
,##9 :
modelId##; B
,##B C
ct##D F
)##F G
;##G H
}$$ 
public&& 

Task&& 
<&& 
LlmResponse&& 
>&& 
GenerateChatAsync&& .
(&&. /
IReadOnlyList'' 
<'' 
HandTurn'' 
>'' 
messages''  (
,''( )
int''* -
	maxTokens''. 7
,''7 8
float''9 >
temperature''? J
,''J K
string(( 
modelId(( 
,(( 
CancellationToken(( )
ct((* ,
=((- .
default((/ 6
)((6 7
{)) 
object** 
[** 
]** 
mapped** 
=** 
messages** "
.++ 
Select++ 
(++ 
m++ 
=>++ 
(++ 
object++  
)++  !
new++! $
{++% &
role++' +
=++, -
m++. /
.++/ 0
Role++0 4
,++4 5
content++6 =
=++> ?
m++@ A
.++A B
Content++B I
}++J K
)++K L
.,, 
ToArray,, 
(,, 
),, 
;,, 
return.. 
	SendAsync.. 
(.. 
mapped.. 
,..  
	maxTokens..! *
,..* +
temperature.., 7
,..7 8
modelId..9 @
,..@ A
ct..B D
)..D E
;..E F
}// 
private11 
async11 
Task11 
<11 
LlmResponse11 "
>11" #
	SendAsync11$ -
(11- .
object22 
[22 
]22 
messages22 
,22 
int22 
	maxTokens22 (
,22( )
float22* /
temperature220 ;
,22; <
string22= C
modelId22D K
,22K L
CancellationToken22M ^
ct22_ a
,22a b
int33 
timeoutSeconds33 
=33 
$num33  
)33  !
{44 
var55 
client55 
=55 
_factory55 
.55 
CreateClient55 *
(55* +
$str55+ 3
)553 4
;554 5
var77 
body77 
=77 
new77 
{88 	
model99 
=99 
modelId99 
,99 
messages:: 
,:: 
stream;; 
=;; 
false;; 
,;; 
options<< 
=<< 
new<< 
{<< 
num_predict<< '
=<<( )
	maxTokens<<* 3
,<<3 4
temperature<<5 @
}<<A B
,<<B C
}== 	
;==	 

using?? 
var?? 

timeoutCts?? 
=?? #
CancellationTokenSource?? 6
.??6 7#
CreateLinkedTokenSource??7 N
(??N O
ct??O Q
)??Q R
;??R S

timeoutCts@@ 
.@@ 
CancelAfter@@ 
(@@ 
TimeSpan@@ '
.@@' (
FromSeconds@@( 3
(@@3 4
timeoutSeconds@@4 B
)@@B C
)@@C D
;@@D E
CancellationTokenAA 
linkedAA  
=AA! "

timeoutCtsAA# -
.AA- .
TokenAA. 3
;AA3 4
tryCC 
{DD 	
usingEE 
HttpResponseMessageEE %
responseEE& .
=EE/ 0
awaitEE1 6
clientEE7 =
.EE= >
PostAsJsonAsyncEE> M
(EEM N
$strEEN Y
,EEY Z
bodyEE[ _
,EE_ `
linkedEEa g
)EEg h
;EEh i
ifFF 
(FF 
!FF 
responseFF 
.FF 
IsSuccessStatusCodeFF -
)FF- .
{GG 
stringHH 
errorHH 
=HH 
awaitHH $
responseHH% -
.HH- .
ContentHH. 5
.HH5 6
ReadAsStringAsyncHH6 G
(HHG H
linkedHHH N
)HHN O
;HHO P
returnII 
newII 
LlmResponseII &
{JJ 
OkKK 
=KK 
falseKK 
,KK 
ErrorLL 
=LL 
$"LL 
$strLL %
{LL% &
responseLL& .
.LL. /

StatusCodeLL/ 9
}LL9 :
$strLL: <
{LL< =
errorLL= B
[LLB C
..LLC E
MathLLE I
.LLI J
MinLLJ M
(LLM N
$numLLN Q
,LLQ R
errorLLS X
.LLX Y
LengthLLY _
)LL_ `
]LL` a
}LLa b
"LLb c
,LLc d
}MM 
;MM 
}NN 
usingPP 
varPP 
docPP 
=PP 
awaitPP !
JsonDocumentPP" .
.PP. /

ParseAsyncPP/ 9
(PP9 :
awaitQQ 
responseQQ 
.QQ 
ContentQQ &
.QQ& '
ReadAsStreamAsyncQQ' 8
(QQ8 9
linkedQQ9 ?
)QQ? @
,QQ@ A
cancellationTokenQQB S
:QQS T
linkedQQU [
)QQ[ \
;QQ\ ]
stringSS 
textSS 
=SS 
docSS 
.SS 
RootElementSS )
.TT 
GetPropertyTT 
(TT 
$strTT &
)TT& '
.UU 
GetPropertyUU 
(UU 
$strUU &
)UU& '
.VV 
	GetStringVV 
(VV 
)VV 
??VV 
stringVV  &
.VV& '
EmptyVV' ,
;VV, -
returnXX 
newXX 
LlmResponseXX "
{XX# $
OkXX% '
=XX( )
trueXX* .
,XX. /
TextXX0 4
=XX5 6
textXX7 ;
.XX; <
TrimXX< @
(XX@ A
)XXA B
,XXB C
ModelIdXXD K
=XXL M
modelIdXXN U
}XXV W
;XXW X
}YY 	
catchZZ 
(ZZ 
	ExceptionZZ 
exZZ 
)ZZ 
whenZZ !
(ZZ" #
exZZ# %
isZZ& ( 
HttpRequestExceptionZZ) =
orZZ> @!
TaskCanceledExceptionZZA V
orZZW Y
JsonExceptionZZZ g
)ZZg h
{[[ 	
return\\ 
new\\ 
LlmResponse\\ "
{\\# $
Ok\\% '
=\\( )
false\\* /
,\\/ 0
Error\\1 6
=\\7 8
ex\\9 ;
.\\; <
Message\\< C
}\\D E
;\\E F
}]] 	
}^^ 
}__ ì

c/home/pablo/Projects/hybrid-therapist/src/HybridTherapist.Infrastructure/Adapters/IOllamaAdapter.cs
	namespace 	
HybridTherapist
 
. 
Infrastructure (
.( )
Adapters) 1
;1 2
public		 
	interface		 
IOllamaAdapter		 
{

 
Task 
< 	
LlmResponse	 
> 
GenerateAsync #
(# $
string 
prompt 
, 
string 
? 
systemPrompt 
, 
int 
	maxTokens 
, 
float 
temperature 
, 
string 
modelId 
, 
CancellationToken 
ct 
= 
default &
)& '
;' (
Task 
< 	
LlmResponse	 
> 
GenerateChatAsync '
(' (
IReadOnlyList 
< 
HandTurn 
> 
messages  (
,( )
int 
	maxTokens 
, 
float 
temperature 
, 
string 
modelId 
, 
CancellationToken 
ct 
= 
default &
)& '
;' (
} 