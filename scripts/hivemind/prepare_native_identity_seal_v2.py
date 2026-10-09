from pathlib import Path
S=Path(__file__).parent
t=(S/'seal_native_identity_contract_v1.py').read_text()
t=t.replace('native-texture-identity-contract-reopen-v1','native-texture-identity-contract-reopen-v2').replace('native-texture-identity-contract-check-v1','native-texture-identity-contract-check-v2').replace('native-texture-identity-contract-candidate-v1','native-texture-identity-contract-candidate-v2')
t=t.replace('29_tests','30_tests').replace('29 passed','30 passed').replace("'tests_passed':29","'tests_passed':30").replace('29 tests','30 tests')
t=t.replace("'original_meta_guid_mutations_refused':30,","'original_meta_guid_mutations_refused':30,'failed_single_resource_rows_refused':10,'unknown_protocols_refused':2,'partial_single_resource_rows_accepted':10,")
t=t.replace('70 mutations refusees','80 mutations de ressources et deux protocoles inconnus refuses').replace('refus 70/70','refus de ressources 80/80 et deux protocoles inconnus')
t=t.replace('verifier la generalite des statuts de capture, ','').replace('review generalized capture status semantics, ','')
t=t.replace('path-only adjustments and native protocol/GUID additions','path adjustments and native protocol/GUID additions, row integrity independent of batch count/status')
(S/'seal_native_identity_contract_v2.py').write_text(t,encoding='utf-8')
