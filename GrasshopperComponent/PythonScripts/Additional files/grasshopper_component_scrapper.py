"""
Grasshopper Component Information Extractor
This script extracts metadata from Grasshopper components and saves it to a JSON file.
Designed to run inside a Grasshopper Python component.
"""

import sys
import os
import json
import traceback
import System
from System import AppDomain
from Grasshopper.Kernel import GH_Component

class CustomJSONEncoder(json.JSONEncoder):
    """Custom JSON encoder to handle non-serializable GH objects."""
    def default(self, obj):
        # Convert any non-serializable objects to strings
        try:
            # Try standard serialization
            return super(CustomJSONEncoder, self).default(obj)
        except TypeError:
            # If that fails, convert to string
            return str(obj)

def get_component_info():
    """Extract information about all available Grasshopper components."""
    
    # List to store all component information
    all_components = []
    error_count = 0
    success_count = 0
    
    print("Looking for Grasshopper assemblies...")
    
    # Get relevant assemblies
    grasshopper_assemblies = []
    for assembly in AppDomain.CurrentDomain.GetAssemblies():
        try:
            name = assembly.GetName().Name
            # Filter for Grasshopper-related assemblies
            if ("Grasshopper" in name or 
                name.startswith("Gh") or 
                "Components" in name):
                grasshopper_assemblies.append(assembly)
                print(f"Found assembly: {name}")
        except Exception as e:
            print(f"Error checking assembly: {str(e)}")
    
    print(f"Found {len(grasshopper_assemblies)} potentially relevant assemblies")
    
    # Process each assembly to find components
    for assembly in grasshopper_assemblies:
        try:
            assembly_name = assembly.GetName().Name
            print(f"Processing assembly: {assembly_name}")
            
            # Try to get all types
            try:
                types = assembly.GetTypes()
            except Exception as e:
                print(f"  Cannot get types from {assembly_name}: {str(e)}")
                continue
                
            # Look for component types
            component_count = 0
            for type in types:
                try:
                    # Check if it's a Grasshopper component
                    if (type.IsSubclassOf(GH_Component) and 
                        not type.IsAbstract and 
                        not type.IsInterface):
                        
                        # Try to get information without instantiation first
                        component_info = {
                            "assembly": assembly_name,
                            "type_name": type.Name,
                            "type_full_name": type.FullName,
                            "inputs": [],
                            "outputs": []
                        }
                        
                        # Try to instantiate the component to get detailed info
                        try:
                            component = System.Activator.CreateInstance(type)
                            
                            # Add detailed information
                            component_info.update({
                                "name": component.Name,
                                "nickname": component.NickName,
                                "description": component.Description,
                                "category": component.Category,
                                "subcategory": component.SubCategory,
                                "guid": str(component.ComponentGuid)
                            })
                            
                            # Get input parameters
                            for i in range(component.Params.Input.Count):
                                input_param = component.Params.Input[i]
                                param_info = {
                                    "name": input_param.Name,
                                    "nickname": input_param.NickName,
                                    "description": input_param.Description,
                                    "type_hint": str(input_param.TypeHint),  # Convert to string
                                    "param_type": input_param.GetType().Name
                                }
                                component_info["inputs"].append(param_info)
                            
                            # Get output parameters
                            for i in range(component.Params.Output.Count):
                                output_param = component.Params.Output[i]
                                param_info = {
                                    "name": output_param.Name,
                                    "nickname": output_param.NickName,
                                    "description": output_param.Description,
                                    "type_hint": str(output_param.TypeHint),  # Convert to string
                                    "param_type": output_param.GetType().Name
                                }
                                component_info["outputs"].append(param_info)
                                
                            success_count += 1
                            
                        except Exception as inst_error:
                            # Failed to instantiate but we still have basic info
                            component_info["instantiation_error"] = str(inst_error)
                            error_count += 1
                        
                        # Add component to our collection
                        all_components.append(component_info)
                        component_count += 1
                        
                except Exception as type_error:
                    continue  # Skip problematic types
                    
            print(f"  Found {component_count} components in {assembly_name}")
                
        except Exception as asm_error:
            print(f"Error processing assembly {assembly_name}: {str(asm_error)}")
    
    print(f"Processing complete. Found {len(all_components)} components.")
    print(f"Successfully instantiated: {success_count}")
    print(f"Failed to instantiate: {error_count}")
    
    return all_components

def export_to_json(components_data):
    """Export the component data to a JSON file."""
    # Try several possible locations
    locations = [
        os.path.join(os.path.expanduser("~"), "Desktop", "grasshopper_components.json"),
        os.path.join(os.environ.get("TEMP", ""), "grasshopper_components.json"),
        "C:\\grasshopper_components.json"  # Root directory (likely to have write permissions)
    ]
    
    error_msgs = []
    
    for file_path in locations:
        try:
            # Ensure path exists
            directory = os.path.dirname(file_path)
            if directory and not os.path.exists(directory):
                os.makedirs(directory)
                
            # Write the file using the custom encoder
            with open(file_path, 'w') as f:
                json.dump(components_data, f, indent=2, cls=CustomJSONEncoder)
                
            print(f"Successfully wrote JSON to: {file_path}")
            return file_path
            
        except Exception as e:
            error_msg = f"Failed to write to {file_path}: {str(e)}"
            error_msgs.append(error_msg)
            print(error_msg)
    
    # If we get here, all attempts failed
    raise Exception(f"Failed to write to any location. Errors: {'; '.join(error_msgs)}")

# Main execution
def main():
    try:
        print("Starting Grasshopper component extraction...")
        print(f"Python version: {sys.version}")
        
        # Extract component information
        components_data = get_component_info()
        
        # Export to JSON
        if components_data:
            file_path = export_to_json(components_data)
            print(f"Component information exported to: {file_path}")
            print(f"Total components extracted: {len(components_data)}")
        else:
            print("No component data was extracted.")
            
    except Exception as e:
        print(f"Critical error in main execution: {str(e)}")
        print(traceback.format_exc())

# Run the script
if __name__ == "__main__" or not __name__:
    main()