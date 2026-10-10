"""Build reviewed node-zone QA presets. Python 3 standard library only.
Run from repository root: python Tools/QA/build_node_suite.py
Optional --source points to a folder containing the five reviewed input files.
The screenshot interpretation is versioned as explicit node membership, not runtime geometry.
"""
import argparse, collections, csv, hashlib, heapq, json, pathlib, re

ROOT = pathlib.Path(__file__).resolve().parents[2]
DOC = ROOT / 'Docs/QA/NodeSuite'
OUT = ROOT / 'Assets/Playtest/Scenarios'

def read_csv(path):
    with path.open(encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))

def layout(path):
    text = path.read_text(encoding='utf-8-sig')
    result = []
    for block in re.findall(r'^    - Id: (.*?)(?=^    - Id: |\Z)', text, re.M | re.S):
        lines = block.splitlines()
        item = {'Id': lines[0], 'Start': bool(int(re.search(r'^      Start: (\d+)$', block, re.M)[1])),
                'X': int(re.search(r'^      X: (-?\d+)$', block, re.M)[1]),
                'Y': int(re.search(r'^      Y: (-?\d+)$', block, re.M)[1]),
                'Links': re.findall(r'^      - (.+)$', block, re.M)}
        result.append(item)
    if not result:
        raise ValueError('NodeCatalog layout not found')
    return result

def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=pathlib.Path)
    args = parser.parse_args()
    source = args.source
    paths = {n: (source/n if source else ROOT/'Assets/Data/NodeTable'/n) for n in ['Nodes.csv','NodeCost.csv','NodeEffects.csv','UpgradeStats.csv']}
    paths['NodeCatalog.asset'] = source/'NodeCatalog.asset' if source else ROOT/'Assets/Data/NodeCatalog.asset'
    nodes = read_csv(paths['Nodes.csv']); costs = read_csv(paths['NodeCost.csv']); effects = read_csv(paths['NodeEffects.csv']); stats = read_csv(paths['UpgradeStats.csv'])
    placed = layout(paths['NodeCatalog.asset']); by_id = {n['Id']: n for n in placed}
    ranks = {n['NodeId']: int(n['Rank 수']) for n in nodes}
    price = {(r['NodeId'],int(r['Rank'])): int(r['Cost'].replace(',','')) for r in costs}
    assert len(by_id)==len(placed)==len(nodes)==len(ranks) and set(by_id)==set(ranks)
    assert len(price)==len(costs) and len(effects)==sum(ranks.values())
    assert set(price)=={(n,r) for n,m in ranks.items() for r in range(1,m+1)}
    adj = {n:set() for n in by_id}
    for n in placed:
        for other in n['Links']:
            assert other in by_id and other!=n['Id']
            adj[n['Id']].add(other); adj[other].add(n['Id'])
    starts = {n['Id'] for n in placed if n['Start']}
    definitions = json.loads((DOC/'zones.json').read_text(encoding='utf-8'))
    zone_of = {}
    for z in definitions['zones']:
        for n in z['nodes']:
            assert n in by_id and n not in zone_of, n
            zone_of[n]=z['id']
    assert set(zone_of)==set(by_id), sorted(set(by_id)-set(zone_of))
    # A minimum additional rank-1 cost path from an owned node or a declared start.
    # Repeating for multiple targets is a deterministic connector heuristic, not a globally optimal tree.
    def connect(owned, target):
        if target in owned: return []
        queue=[]; best={}
        for n in sorted(starts | set(owned)):
            c=0 if n in owned else price[n,1]
            heapq.heappush(queue,(c,(n,),n))
        while queue:
            c,path,n=heapq.heappop(queue)
            if n in best: continue
            best[n]=c
            if n==target: return [v for v in path if v not in owned]
            for other in sorted(adj[n]):
                if other not in best:
                    heapq.heappush(queue,(c+(0 if other in owned else price[other,1]),path+(other,),other))
        raise ValueError('Unreachable target '+target)
    manifest=[]; rows=[]
    def cost_of(owned): return sum(price[n,r] for n,m in owned.items() for r in range(1,m+1))
    def emit(name, category, owned, stage, level, frozen, expected, note, budget=0,gold=0,extra=None):
        scenario={'name':name,'category':category,'note':note,'expected':expected,'growthStage':stage,'gold':gold,
                  'nodes':[{'nodeId':n,'rank':m} for n,m in sorted(owned.items())],
                  'autoBuyBudget':budget,'startLevel':level,'seed':20261011,'freezeTime':frozen}
        write_json(OUT/(name+'.json'),scenario)
        manifest.append({'name':name,'category':category,'ownedCount':len(owned),'rankCount':sum(owned.values()),'purchaseCost':cost_of(owned),
                         'stage':stage,'level':level,'freezeTime':frozen,'budget':budget,'gold':gold, **(extra or {})})
    emit('qa-node-first','QA/노드/기준',{},0,0,False,'노드 없음; 12초 기본 시간; 소행성 시작 공급 20; 결산과 재시작 확인','첨부 UpgradeStats의 기본값과 게임 기본 에셋을 대조하는 기준 판.')
    for z in definitions['zones']:
        number=z['id']; target=z['focus']; owned={}; order=[]
        for n in z['support']+[target]:
            for connector in connect(owned,n):
                owned[connector]=1; order.append(connector)
        assert order[-1]==target, (target,order)
        before=dict(owned); del before[target]
        category=f"QA/구역{number}/단일비교"
        for suffix,o in [('off',before),('on',owned)]:
            emit(f'qa-zone{number:02d}-{suffix}',category,o,z['stage'],0,True,z['expected'],
                 f"이미지 구역{number}: {z['title']}. off/on은 {target} Rank1 하나만 다름. 연결용 노드 포함; 시간 고정은 관찰용.",extra={'focus':target,'purchaseOrder':[n for n in order if n in o]})
        all_owned={}; full_order=[]
        for n in z['support']+sorted(z['nodes']):
            for connector in connect(all_owned,n):
                all_owned[connector]=1;full_order.append(connector)
        for n in z['nodes']: all_owned[n]=ranks[n]
        emit(f'qa-zone{number:02d}-full',f'QA/구역{number}/최대',all_owned,3,0,True,
             '구역 내 모든 Rank 적용; 확정 정보·효과 중첩·화면·사운드·프레임을 확인. 재미와 자연 진행 판정에는 사용하지 않음.',
             f"구역{number} 최대 기능 부하. 다른 구역의 연결 노드는 Rank1만 포함. 단계3은 이정표 자동 종료를 피하기 위한 관찰 조건이며 자연 도달 시점을 뜻하지 않음.",extra={'zoneNodes':z['nodes'],'connectors':sorted(set(all_owned)-set(z['nodes']))})
        rows.append({'id':number,'title':z['title'],'nodeCount':len(z['nodes']),'rankCount':sum(ranks[n] for n in z['nodes']),
                     'zoneCost':cost_of({n:ranks[n] for n in z['nodes']}),'focus':target,'focusCost':price[target,1],
                     'focusEffects':[r for r in effects if r['NodeId']==target and r['Rank']=='1'],
                     'offCost':cost_of(before),'onCost':cost_of(owned),'fullCost':cost_of(all_owned),
                     'bounds':{'minX':min(by_id[n]['X'] for n in z['nodes']),'maxX':max(by_id[n]['X'] for n in z['nodes']),
                               'minY':min(by_id[n]['Y'] for n in z['nodes']),'maxY':max(by_id[n]['Y'] for n in z['nodes'])}})
    # Global cheapest legal purchase route, including later ranks; equal costs use ordinal NodeId.
    route=[]; owned={}; spent=0; thresholds={}
    remaining=sum(ranks.values())
    for _ in range(remaining):
        choices=[(price[n,owned.get(n,0)+1],n) for n in by_id if owned.get(n,0)<ranks[n] and (n in starts or any(v in owned for v in adj[n]))]
        c,n=min(choices);owned[n]=owned.get(n,0)+1;spent+=c
        route.append({'nodeId':n,'rank':owned[n],'cost':c,'spent':spent,'zone':zone_of[n]})
        for z in definitions['zones']:
            if z['focus']==n and owned[n]==1: thresholds[z['id']]=(spent,dict(owned))
    assert len(route)==sum(ranks.values())
    for z in definitions['zones']:
        budget,resolved=thresholds[z['id']]
        emit(f"qa-budget-zone{z['id']:02d}",'QA/예산',{},z['stage'],0,False,
             f"예산 {budget:,}: 전역 최저가 순서에서 {z['focus']} Rank1이 처음 포함되는 지점. 생성 후 Rank 목록 비교.",
             '구역 한정 구매가 아님. 모든 드러난 노드 중 최저가를 구매. 콘텐츠가 바뀌면 결과도 바뀌는 레시피.',budget=budget,
             extra={'resolvedNodes':[{'nodeId':n,'rank':r} for n,r in sorted(resolved.items())]})
    growth_text=(ROOT/'Assets/Data/HqGrowthSetup.asset').read_text()
    marks=[{'level':int(l),'targetGold':int(g),'fieldScale':float(s)} for l,g,s in re.findall(r'- level: (\d+)\s+targetGold: (\d+)\s+fieldScale: ([\d.]+)',growth_text)]
    for index,mark in enumerate(marks):
        candidates=[r for r in route if r['spent']<=mark['targetGold']]
        snapshot={}
        for r in candidates:snapshot[r['nodeId']]=r['rank']
        # Before/after use exactly the same ranks and Gold; only stage and starting Level differ.
        for phase,stage,level in [('before',index,mark['level']-1),('after',index+1,mark['level'])]:
            emit(f"qa-mark{mark['level']:02d}-{phase}",'QA/이정표/동일노드',snapshot,stage,level,False,
                 f"Level {mark['level']} 경계: 판 종료·성장 단계·결산 목표 잔액 {mark['targetGold']:,}; 다음 판 전장 배율 {mark['fieldScale']} 확인.",
                 f"노드는 목표 잔액 {mark['targetGold']:,}을 구매 예산으로 해석한 비교용 가정. 보상은 실제로 목표 잔액이며 구매 예산과 동일 개념이 아님. before/after의 노드와 Gold는 동일.",gold=0,
                 extra={'nodeBudgetAssumption':mark['targetGold'],'targetGold':mark['targetGold'],'nextFieldScale':mark['fieldScale']})
    refs={}
    for r in stats:
        enum=''.join(p[0].upper()+p[1:] for p in r['StatId'].split('.'))
        hits=[]
        for path in (ROOT/'Assets/Scripts/Core').rglob('*.cs'):
            for line_no,line in enumerate(path.read_text().splitlines(),1):
                if re.search(r'UpgradeStat\.'+enum+r'\b',line): hits.append({'file':str(path.relative_to(ROOT)),'line':line_no,'code':line.strip()})
        refs[r['StatId']]=hits
    edge_count=sum(map(len,adj.values()))//2
    inputs={n:{'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'rows':len(read_csv(p)) if n.endswith('.csv') else len(placed)} for n,p in paths.items()}
    inputs['HqGrowthSetup.asset']={'sha256':hashlib.sha256((ROOT/'Assets/Data/HqGrowthSetup.asset').read_bytes()).hexdigest(),'note':'첨부에 없어 게임 레포에서 추가로 확인한 성장 에셋'}
    analysis={'schema':1,'sourceInputs':inputs,'counts':{'nodes':len(nodes),'ranks':sum(ranks.values()),'stats':len(stats),'edges':edge_count},
              'starts':sorted(starts),'totalPurchaseCost':spent,'zones':rows,'milestones':marks,'runtimeStatReferences':refs,
              'presets':manifest,'cheapestPurchaseRoute':route}
    write_json(DOC/'analysis.json',analysis)
    ladder = re.search(r'levelExp: ([0-9a-f]+)', growth_text)[1]
    exps = [int.from_bytes(bytes.fromhex(ladder[i:i+16]), 'little', signed=True) for i in range(0,len(ladder),16)]
    assert all(a < b for a,b in zip([0]+exps,exps))
    write_json(DOC/'layout.fixture.json',{'Nodes':placed,'Milestones':marks,'LevelExp':exps})
    print(json.dumps({'counts':analysis['counts'],'presets':len(manifest),'starts':analysis['starts'],'zones':rows},ensure_ascii=False,indent=2))

if __name__=='__main__':main()
