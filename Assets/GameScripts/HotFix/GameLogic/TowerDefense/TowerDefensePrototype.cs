using System.Collections.Generic;
using UnityEngine;
namespace GameLogic
{
 public static class TowerDefenseBootstrap
 {
  static GameObject _root;
  public static void Start(){if(_root==null){_root=new GameObject("TowerDefensePrototype");_root.AddComponent<TowerDefenseGame>();}}
  public static void Stop(){if(_root!=null)Object.Destroy(_root);_root=null;}
 }
 public sealed class TowerDefenseGame:MonoBehaviour
 {
  const int N=20;
  enum Mode{Wall,Arrow,Cannon,Ice,Demolish}
  enum MobType{Grunt,Runner,Heavy}
  struct Cell{public int X,Y;public Cell(int x,int y){X=x;Y=y;}}
  sealed class Building{public Mode Type;public Cell Cell;public int Cost;public float Hp,MaxHp,Cooldown;public GameObject View;public Transform Bar;}
  sealed class Mob{public MobType Type;public float Hp,MaxHp,Speed,WallDps,Slow=1,SlowTime,Repath;public int Damage,Reward,PathIndex,Revision=-1;public Vector2 Pos;public List<Cell> Path;public GameObject View;public Transform Bar;public bool Dead;}
  sealed class Wave{public readonly List<MobType>Mobs=new List<MobType>();public int Reward;}
  readonly Building[,]_map=new Building[N,N];readonly List<Building>_towers=new List<Building>();readonly List<Mob>_mobs=new List<Mob>();
  readonly List<Wave>_waves=new List<Wave>();readonly Queue<MobType>_queue=new Queue<MobType>();readonly Cell _spawn=new Cell(0,10),_goal=new Cell(19,10);
  Transform _buildRoot,_mobRoot;Sprite _sprite;Camera _camera;Mode _mode;int _gold,_baseHp,_wave,_revision;float _spawnTimer;bool _fighting,_ended,_won;int _speed=1,_lastPaintX=-99,_lastPaintY=-99;string _message;
  void Awake()
  {
   _sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);Transform board=Root("Board");_buildRoot=Root("Buildings");_mobRoot=Root("Enemies");
   _camera=Camera.main;if(_camera==null){GameObject c=new GameObject("Main Camera");c.tag="MainCamera";_camera=c.AddComponent<Camera>();}
   _camera.orthographic=true;_camera.orthographicSize=11.2f;_camera.transform.position=new Vector3(9.5f,9.5f,-10);_camera.backgroundColor=new Color(.055f,.07f,.09f);
   for(int y=0;y<N;y++)for(int x=0;x<N;x++){Color c=(x+y)%2==0?new Color(.16f,.20f,.22f):new Color(.19f,.23f,.25f);if(x==_spawn.X&&y==_spawn.Y)c=new Color(.2f,.65f,.3f);if(x==_goal.X&&y==_goal.Y)c=new Color(.75f,.22f,.18f);SpriteObj("Cell",new Vector3(x,y,1),Vector2.one*.94f,c,board);}
   MakeWaves();ResetGame();
  }
  Transform Root(string n){Transform t=new GameObject(n).transform;t.SetParent(transform,false);return t;}
  GameObject SpriteObj(string n,Vector3 p,Vector2 s,Color c,Transform parent){GameObject o=new GameObject(n);o.transform.SetParent(parent,false);o.transform.position=p;o.transform.localScale=new Vector3(s.x,s.y,1);SpriteRenderer r=o.AddComponent<SpriteRenderer>();r.sprite=_sprite;r.color=c;return o;}
  Transform AddBar(GameObject owner){GameObject b=SpriteObj("HpBack",Vector3.zero,new Vector2(.84f,.1f),new Color(.12f,.03f,.03f),owner.transform);b.transform.localPosition=new Vector3(0,.58f,-.1f);GameObject f=SpriteObj("Hp",Vector3.zero,Vector2.one,new Color(.25f,.95f,.35f),b.transform);f.transform.localPosition=new Vector3(0,0,-.1f);return f.transform;}
  static void Bar(Transform b,float v){v=Mathf.Clamp01(v);b.localScale=new Vector3(v,1,1);b.localPosition=new Vector3((v-1)*.5f,0,-.1f);}
  void Update()
  {
   if(_ended)return;InputUpdate();SpawnUpdate(Time.deltaTime);MobUpdate(Time.deltaTime);TowerUpdate(Time.deltaTime);
   if(_fighting&&_queue.Count==0&&_mobs.Count==0){_fighting=false;_gold+=_waves[_wave].Reward;_wave++;if(_wave==10){_ended=_won=true;_message="防线经受住全部 10 波，胜利！";}else _message="波次清除，可以继续调整防线";}
  }
  void InputUpdate()
  {
   if(Input.GetMouseButtonUp(0)){_lastPaintX=_lastPaintY=-99;return;}bool painting=_mode==Mode.Wall?Input.GetMouseButton(0):Input.GetMouseButtonDown(0);if(!painting||Input.mousePosition.x>Screen.width-225||Input.mousePosition.y>Screen.height-120)return;Vector3 p=_camera.ScreenToWorldPoint(Input.mousePosition);int cellX=Mathf.RoundToInt(p.x),cellY=Mathf.RoundToInt(p.y);
   if(!Inside(cellX,cellY))return;if(_mode==Mode.Demolish){Demolish(cellX,cellY);return;}int x=Mathf.Clamp(Mathf.RoundToInt(p.x-.5f),0,N-2),y=Mathf.Clamp(Mathf.RoundToInt(p.y-.5f),0,N-2);if(_mode==Mode.Wall&&x==_lastPaintX&&y==_lastPaintY)return;_lastPaintX=x;_lastPaintY=y;Build(x,y);
  }
  void Build(int x,int y)
  {
   Building foundation=null;if(_mode==Mode.Wall){if(!CanBuild(x,y)){_message="需要一块空闲的 2×2 区域";return;}}else{foundation=WallFoundation(x,y);if(foundation==null){_message="防御塔必须建在完整的 2×2 墙块上";return;}}int cost=_mode==Mode.Wall?10:_mode==Mode.Arrow?50:_mode==Mode.Cannon?85:65;if(_gold<cost){_message="金币不足";return;}if(foundation!=null)RemoveBuilding(foundation);
   float hp=_mode==Mode.Wall?140:_mode==Mode.Arrow?90:_mode==Mode.Cannon?110:85;Color c=_mode==Mode.Wall?new Color(.48f,.52f,.56f):_mode==Mode.Arrow?new Color(.2f,.55f,1):_mode==Mode.Cannon?new Color(1,.55f,.15f):new Color(.25f,.92f,.95f);
   Building b=new Building{Type=_mode,Cell=new Cell(x,y),Cost=cost,Hp=hp,MaxHp=hp};b.View=SpriteObj(_mode.ToString(),new Vector3(x+.5f,y+.5f,-.5f),Vector2.one*1.78f,c,_buildRoot);b.Bar=AddBar(b.View);for(int oy=0;oy<2;oy++)for(int ox=0;ox<2;ox++)_map[x+ox,y+oy]=b;if(_mode!=Mode.Wall)_towers.Add(b);_gold-=cost;_revision++;_message="建造完成";
  }
  void Demolish(int x,int y){Building b=_map[x,y];if(b==null)return;_gold+=Mathf.FloorToInt(b.Cost*.7f);RemoveBuilding(b);_message="已拆除，返还 70% 金币";}
  void RemoveBuilding(Building b){if(_map[b.Cell.X,b.Cell.Y]!=b)return;for(int oy=0;oy<2;oy++)for(int ox=0;ox<2;ox++)if(_map[b.Cell.X+ox,b.Cell.Y+oy]==b)_map[b.Cell.X+ox,b.Cell.Y+oy]=null;_towers.Remove(b);if(b.View!=null)Destroy(b.View);_revision++;}
  void BeginWave(){if(_fighting||_ended)return;foreach(MobType t in _waves[_wave].Mobs)_queue.Enqueue(t);_fighting=true;_spawnTimer=0;_message="第 "+(_wave+1)+" 波来袭";}
  void SpawnUpdate(float dt){if(!_fighting||_queue.Count==0)return;_spawnTimer-=dt;if(_spawnTimer>0)return;Spawn(_queue.Dequeue());_spawnTimer=.55f;}
  void Spawn(MobType type)
  {
   Mob m=new Mob{Type=type,Pos=new Vector2(_spawn.X,_spawn.Y)};if(type==MobType.Runner){m.MaxHp=24;m.Speed=1.6f;m.WallDps=4;m.Damage=1;m.Reward=9;}else if(type==MobType.Heavy){m.MaxHp=140;m.Speed=.6f;m.WallDps=12;m.Damage=2;m.Reward=20;}else{m.MaxHp=38;m.Speed=.9f;m.WallDps=5;m.Damage=1;m.Reward=8;}m.Hp=m.MaxHp;
   Color c=type==MobType.Grunt?new Color(.95f,.25f,.22f):type==MobType.Runner?new Color(1,.25f,.7f):new Color(.55f,.18f,.7f);m.View=SpriteObj(type.ToString(),new Vector3(m.Pos.x,m.Pos.y,-2),Vector2.one*.72f,c,_mobRoot);m.Bar=AddBar(m.View);_mobs.Add(m);Repath(m);
  }
  void MobUpdate(float dt)
  {
   for(int i=_mobs.Count-1;i>=0;i--){Mob m=_mobs[i];if(m.Dead){RemoveMob(i);continue;}m.SlowTime-=dt;if(m.SlowTime<=0)m.Slow=1;m.Repath-=dt;if(m.Path==null||m.Revision!=_revision||m.Repath<=0)Repath(m);if(m.Path==null||m.PathIndex>=m.Path.Count)continue;
    Cell next=m.Path[m.PathIndex];Building b=ObstacleAt(next);if(b!=null){b.Hp-=m.WallDps*dt;Bar(b.Bar,b.Hp/b.MaxHp);if(b.Hp<=0)RemoveBuilding(b);continue;}Vector2 target=new Vector2(next.X,next.Y);m.Pos=Vector2.MoveTowards(m.Pos,target,m.Speed*m.Slow*dt);m.View.transform.position=new Vector3(m.Pos.x,m.Pos.y,-2);
    if((m.Pos-target).sqrMagnitude>.0025f)continue;m.PathIndex++;if(next.X==_goal.X&&next.Y==_goal.Y){_baseHp-=m.Damage;RemoveMob(i);_message="怪物突破，基地受到 "+m.Damage+" 点伤害";if(_baseHp<=0){_baseHp=0;_ended=true;_message="基地被攻破";}}}
  }
  void TowerUpdate(float dt)
  {
   for(int i=_towers.Count-1;i>=0;i--){Building t=_towers[i];if(_map[t.Cell.X,t.Cell.Y]!=t){_towers.RemoveAt(i);continue;}t.Cooldown-=dt;if(t.Cooldown>0)continue;float range=t.Type==Mode.Arrow?4:t.Type==Mode.Cannon?3.5f:3;Mob target=Target(new Vector2(t.Cell.X+.5f,t.Cell.Y+.5f),range);if(target==null)continue;
    if(t.Type==Mode.Arrow){Hit(target,14);t.Cooldown=.85f;}else if(t.Type==Mode.Cannon){Vector2 center=target.Pos;for(int j=_mobs.Count-1;j>=0;j--)if(!_mobs[j].Dead&&(_mobs[j].Pos-center).sqrMagnitude<=1.44f)Hit(_mobs[j],32);t.Cooldown=1.7f;}else{Hit(target,6);target.Slow=.6f;target.SlowTime=1.8f;t.Cooldown=1.1f;}}
  }
  Mob Target(Vector2 p,float range){Mob target=null;float best=range*range;foreach(Mob m in _mobs){float d=(m.Pos-p).sqrMagnitude;if(!m.Dead&&d<=best){best=d;target=m;}}return target;}
  void Hit(Mob m,float d){if(m.Dead)return;m.Hp-=d;Bar(m.Bar,m.Hp/m.MaxHp);if(m.Hp<=0){m.Dead=true;_gold+=m.Reward;}}
  void RemoveMob(int i){if(_mobs[i].View!=null)Destroy(_mobs[i].View);_mobs.RemoveAt(i);}
  void Repath(Mob m){Cell s=new Cell(Mathf.Clamp(Mathf.RoundToInt(m.Pos.x),0,19),Mathf.Clamp(Mathf.RoundToInt(m.Pos.y),0,19));m.Path=Path(s,m);m.PathIndex=m.Path!=null&&m.Path.Count>1?1:0;m.Revision=_revision;m.Repath=.75f;}
  List<Cell> Path(Cell start,Mob m)
  {
   float[,]dist=new float[N,N];bool[,]used=new bool[N,N],has=new bool[N,N];Cell[,]prev=new Cell[N,N];for(int y=0;y<N;y++)for(int x=0;x<N;x++)dist[x,y]=float.MaxValue;dist[start.X,start.Y]=0;int[]dx={1,-1,0,0,1,1,-1,-1},dy={0,0,1,-1,1,-1,1,-1};
   for(int n=0;n<N*N;n++){int cx=-1,cy=-1;float best=float.MaxValue;for(int y=0;y<N;y++)for(int x=0;x<N;x++)if(!used[x,y]&&dist[x,y]<best){best=dist[x,y];cx=x;cy=y;}if(cx<0||(cx==_goal.X&&cy==_goal.Y))break;used[cx,cy]=true;
    for(int k=0;k<8;k++){int x=cx+dx[k],y=cy+dy[k];if(!Inside(x,y)||used[x,y])continue;float cost=(k<4?1f:1.4142f)/m.Speed;Building obstacle=ObstacleAt(new Cell(x,y));if(obstacle!=null)cost+=obstacle.Hp/Mathf.Max(1,m.WallDps);if(best+cost>=dist[x,y])continue;dist[x,y]=best+cost;prev[x,y]=new Cell(cx,cy);has[x,y]=true;}}
   if((start.X!=_goal.X||start.Y!=_goal.Y)&&!has[_goal.X,_goal.Y])return null;List<Cell>path=new List<Cell>();Cell cur=_goal;path.Add(cur);while(cur.X!=start.X||cur.Y!=start.Y){cur=prev[cur.X,cur.Y];path.Add(cur);}path.Reverse();return path;
  }
  void MakeWaves(){AddWave(40,6,0,0);AddWave(45,9,0,0);AddWave(50,6,3,0);AddWave(55,0,8,0);AddWave(60,9,0,2);AddWave(70,0,8,3);AddWave(80,14,6,0);AddWave(90,8,8,4);AddWave(100,0,14,6);AddWave(120,14,10,7);}
  void AddWave(int reward,int a,int b,int c){Wave w=new Wave{Reward=reward};int n=Mathf.Max(a,Mathf.Max(b,c));for(int i=0;i<n;i++){if(i<a)w.Mobs.Add(MobType.Grunt);if(i<b)w.Mobs.Add(MobType.Runner);if(i<c)w.Mobs.Add(MobType.Heavy);}_waves.Add(w);}
  void ResetGame(){for(int y=0;y<N;y++)for(int x=0;x<N;x++){Building b=_map[x,y];if(b==null)continue;if(b.View!=null)Destroy(b.View);for(int oy=0;oy<2;oy++)for(int ox=0;ox<2;ox++)if(b.Cell.X+ox<N&&b.Cell.Y+oy<N&&_map[b.Cell.X+ox,b.Cell.Y+oy]==b)_map[b.Cell.X+ox,b.Cell.Y+oy]=null;}foreach(Mob m in _mobs)if(m.View!=null)Destroy(m.View);_towers.Clear();_mobs.Clear();_queue.Clear();_gold=500;_baseHp=30;_wave=0;_revision++;_fighting=_ended=_won=false;_speed=1;Time.timeScale=1;_message="先设计防线，然后开始第 1 波";}
  Building WallFoundation(int x,int y){if(!AnchorInside(x,y))return null;Building b=_map[x,y];if(b==null||b.Type!=Mode.Wall)return null;for(int oy=0;oy<2;oy++)for(int ox=0;ox<2;ox++)if(_map[x+ox,y+oy]!=b)return null;return b;} bool CanBuild(int x,int y){if(!AnchorInside(x,y))return false;for(int oy=0;oy<2;oy++)for(int ox=0;ox<2;ox++){int px=x+ox,py=y+oy;if(_map[px,py]!=null||(px<2&&py>=9&&py<11)||(px>=18&&py>=9&&py<11))return false;}return true;} Building ObstacleAt(Cell c){return _map[c.X,c.Y];} static bool Inside(int x,int y){return x>=0&&y>=0&&x<N&&y<N;} static bool AnchorInside(int x,int y){return x>=0&&y>=0&&x<N-1&&y<N-1;}
  void OnGUI()
  {
   GUI.Box(new Rect(10,10,Mathf.Min(660,Screen.width-250),82),"");GUI.Label(new Rect(22,16,630,24),"基地 "+_baseHp+"/30    金币 "+_gold+"    已完成 "+_wave+"/10 波    敌人 "+_mobs.Count);GUI.Label(new Rect(22,42,630,22),_message);GUI.Label(new Rect(22,64,630,20),"按住鼠标连续画墙；防御塔只能在完整墙块上改建。");
   float x=Screen.width-215;GUI.Box(new Rect(x-10,10,205,323),"建造菜单");Btn(new Rect(x,38,185,30),Mode.Wall,"墙  $10  按住拖画");Btn(new Rect(x,72,185,30),Mode.Arrow,"箭塔  $50  单体");Btn(new Rect(x,106,185,30),Mode.Cannon,"炮塔  $85  范围");Btn(new Rect(x,140,185,30),Mode.Ice,"冰塔  $65  减速");Btn(new Rect(x,174,185,30),Mode.Demolish,"拆除  返还70%");
   GUI.enabled=!_fighting&&!_ended;if(GUI.Button(new Rect(x,214,185,34),"开始第 "+Mathf.Min(_wave+1,10)+" 波"))BeginWave();GUI.enabled=true;if(GUI.Button(new Rect(x,252,58,30),"1×"))SetSpeed(1);if(GUI.Button(new Rect(x+62,252,58,30),"2×"))SetSpeed(2);if(GUI.Button(new Rect(x+124,252,61,30),"4×"))SetSpeed(4);if(GUI.Button(new Rect(x,286,185,30),"重新开始"))ResetGame();
   if(_ended){GUI.Box(new Rect(Screen.width/2-170,Screen.height/2-55,340,110),_won?"胜利":"失败");GUI.Label(new Rect(Screen.width/2-145,Screen.height/2-18,290,28),_message);if(GUI.Button(new Rect(Screen.width/2-80,Screen.height/2+15,160,28),"再来一局"))ResetGame();}
  }
  void SetSpeed(int speed){_speed=speed;Time.timeScale=speed;_message="战斗速度切换为 "+speed+"×";} void OnDestroy(){Time.timeScale=1;} void Btn(Rect r,Mode m,string s){Color old=GUI.color;if(_mode==m)GUI.color=new Color(.55f,1,.65f);if(GUI.Button(r,s))_mode=m;GUI.color=old;}
 }
}