import json

path_target_file = r'C:\Users\terei\NTerei\Programmierung\SolidWorks_ASsembly_Instructor\SolidWorks_ASsembly_Instructor\examples\SWASI_6D_Example_Ideal\SWASI_Exports\components\Glas_6D_ideal.json'
path_source_file = r'C:\Users\terei\NTerei\Programmierung\SolidWorks_ASsembly_Instructor\SolidWorks_ASsembly_Instructor\examples\SWASI_6D_Example\SWASI_Exports\components\Glas_6D_tol.json'

path_target_file_test = r'C:\Users\terei\NTerei\Programmierung\SolidWorks_ASsembly_Instructor\SolidWorks_ASsembly_Instructor\examples\SWASI_6D_Example_Ideal\SWASI_Exports\components\Glas_6D_ideal_test.json'

# open json file in path
with open(path_target_file, 'r') as file:
    target_dict = json.load(file)

with open(path_source_file, 'r') as file:
    source_dict = json.load(file)

def set_constraints_dict_for_frame(frame_name:str, 
                                   target_dict_frames:dict,
                                   constraints_dict:dict)->bool:
    for frame in target_dict_frames:
        if frame['name'] == frame_name:
            frame['constraints'] = constraints_dict
            return True
    return False

target_mountingDescription = target_dict.get('mountingDescription')
source_mountingDescription = source_dict.get('mountingDescription')

target_mountingReferences = target_mountingDescription.get('mountingReferences')
source_mountingReferences = source_mountingDescription.get('mountingReferences')

target_ref_frames = target_mountingReferences.get('ref_frames')
source_ref_frames = source_mountingReferences.get('ref_frames')

copy_missing_frames = True

for frame in source_ref_frames:
    constraints_dict = frame.get('constraints')
    set_success = set_constraints_dict_for_frame(frame['name'], target_ref_frames, constraints_dict)
    if not set_success:
        print(f"Frame {frame['name']} not found in target file")
        if copy_missing_frames:
            target_ref_frames.append(frame)
            print(f"Frame {frame['name']} added to target file")

# save the updated dict to the target file
with open(path_target_file, 'w') as file:
    json.dump(target_dict, file, indent=4)
