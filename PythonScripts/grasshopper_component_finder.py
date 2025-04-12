#!/usr/bin/env python3
"""
Grasshopper Component Finder

This script uses an LLM to suggest appropriate Grasshopper components for a given task.
It takes a natural language prompt and returns component suggestions with explanations.

Usage:
    python grasshopper_component_finder.py --prompt "Create a grid of circles" --components "path/to/components.json" --api-key "your_api_key"

Requirements:
    - requests
    - argparse
"""

import json
import requests
import os
import sys
import argparse

def load_component_database(file_path):
    """Load component information from JSON file"""
    try:
        if not os.path.exists(file_path):
            return {"error": f"File not found: {file_path}"}
        
        with open(file_path, 'r') as f:
            components = json.load(f)
        
        # Check if we have a meaningful JSON structure
        if isinstance(components, list) and len(components) > 0:
            print(f"Loaded {len(components)} components")
            return {"components": components}
        else:
            return {"error": "Invalid component data structure"}
    except Exception as e:
        return {"error": f"Error loading component database: {str(e)}"}

def select_relevant_components(components, prompt, max_components=15):
    """
    Select the most relevant components based on the prompt.
    This is a simple keyword matching approach.
    A more sophisticated approach could use embeddings for semantic matching.
    """
    # Define some common keywords that might appear in prompts
    keyword_map = {
        "circle": ["Circle", "Radius", "Center"],
        "line": ["Line", "Start", "End"],
        "point": ["Point", "Construct Point"],
        "curve": ["Curve", "Interpolate", "Divide Curve"],
        "surface": ["Surface", "Boundary Surface", "Extrude"],
        "move": ["Move", "Transform"],
        "rotate": ["Rotate", "Orient"],
        "scale": ["Scale", "Transform"],
        "slider": ["Number Slider", "Slider"],
        "panel": ["Panel"],
        "divide": ["Divide", "Divide Curve", "Split"],
        "boolean": ["Boolean", "Difference", "Union", "Intersection"],
        "intersection": ["Intersection", "Brep|Brep", "Curve|Curve"],
        "extrude": ["Extrude", "ExtrudeCrv"],
        "loft": ["Loft", "LoftSrf"],
        "random": ["Random", "Jitter", "Populate"],
        "grid": ["Grid", "Rectangular", "Hexagonal"],
        "vector": ["Vector", "Direction", "Vector XYZ"],
        "array": ["Series", "Range", "Repeat"],
        "color": ["Color", "Gradient", "ColourRGB"],
        "mesh": ["Mesh", "MeshSrf", "MeshPlane"],
        "text": ["Text", "TextTag", "FontList"],
    }
    
    # Convert prompt to lowercase for case-insensitive matching
    prompt_lower = prompt.lower()
    
    # Score components based on keyword relevance
    scored_components = []
    for component in components:
        score = 0
        component_name = component.get("name", "").lower()
        component_description = component.get("description", "").lower()
        
        # Check if component name or description directly contains words from the prompt
        for word in prompt_lower.split():
            if word in component_name or word in component_description:
                score += 3
        
        # Check for relevant keywords
        for keyword, related_components in keyword_map.items():
            if keyword in prompt_lower:
                for related in related_components:
                    if related.lower() in component_name:
                        score += 5
        
        # Only include components with a positive score
        if score > 0:
            scored_components.append((score, component))
    
    # Sort by score (descending) and take top N
    scored_components.sort(reverse=True, key=lambda x: x[0])
    return [component for score, component in scored_components[:max_components]]

def call_llm_api(prompt, components_data, api_key):
    """Call LLM API with the prompt and component information"""
    # You can replace this with any LLM API you have access to
    # This example uses Anthropic's Claude API
    url = "https://api.anthropic.com/v1/messages"
    headers = {
        "x-api-key": api_key,
        "anthropic-version": "2023-06-01",
        "content-type": "application/json"
    }
    
    if "error" in components_data:
        # If we couldn't load components, let the LLM know
        components_info = f"[ERROR LOADING COMPONENTS: {components_data['error']}]"
        relevant_components = []
    else:
        # Find relevant components based on the prompt
        all_components = components_data.get("components", [])
        relevant_components = select_relevant_components(all_components, prompt)
        
        # Format component information for the prompt
        components_info = []
        for comp in relevant_components:
            comp_info = f"Component: {comp.get('name', 'Unknown')} (Category: {comp.get('category', 'Unknown')}"
            if comp.get('subcategory'):
                comp_info += f", Subcategory: {comp.get('subcategory')}"
            comp_info += ")\n"
            
            if comp.get('description'):
                comp_info += f"Description: {comp.get('description')}\n"
            
            # Format inputs
            inputs = []
            for inp in comp.get('inputs', []):
                if isinstance(inp, dict):
                    input_name = inp.get('name', 'Unknown')
                    input_desc = inp.get('description', '')
                    input_type = inp.get('type', '')
                    inputs.append(f"{input_name} ({input_type}): {input_desc}")
                else:
                    inputs.append(str(inp))
            
            if inputs:
                comp_info += f"Inputs: {', '.join(inputs)}\n"
            
            # Format outputs
            outputs = []
            for out in comp.get('outputs', []):
                if isinstance(out, dict):
                    output_name = out.get('name', 'Unknown')
                    output_desc = out.get('description', '')
                    output_type = out.get('type', '')
                    outputs.append(f"{output_name} ({output_type}): {output_desc}")
                else:
                    outputs.append(str(out))
            
            if outputs:
                comp_info += f"Outputs: {', '.join(outputs)}\n"
            
            components_info.append(comp_info)
        
        components_info = "\n".join(components_info)
    
    # Create the system prompt with component information
    system_prompt = f"""You are an assistant that helps users find and use the right Grasshopper components.
When given a task description, suggest the appropriate Grasshopper components to accomplish it.
Only suggest components that exist in the Grasshopper ecosystem.

Here are Grasshopper components that might be relevant to the user's request:

{components_info}

Your task is to provide a JSON response that a C# program can use to automatically create and connect 
Grasshopper components on the canvas. Your response MUST include a valid JSON object with this structure:

```json
{{
  "explanation": "A brief explanation of the approach",
  "components": [
    {{
      "id": "unique_id_1",
      "name": "ComponentName",
      "category": "Category",
      "subcategory": "Subcategory",
      "position": {{
        "x": 0,
        "y": 0
      }},
      "parameters": [
        {{
          "name": "ParameterName",
          "value": "Value"
        }}
      ]
    }}
  ],
  "connections": [
    {{
      "fromComponent": "unique_id_1",
      "fromOutput": "OutputName",
      "toComponent": "unique_id_2",
      "toInput": "InputName"
    }}
  ]
}}
```

Follow these guidelines:
1. Only suggest components that exist in the provided list
2. Position components logically on the canvas (left to right, data flow)
3. Make sure all connections are valid (outputs connect to appropriate inputs)
4. For simple number parameters, use the "parameters" field
5. Canvas positions should use relative coordinates, with the first component at (0,0) and subsequent components at reasonable distances (e.g., 100 units apart)

Be sure your output is a VALID JSON object. Do not include any text before or after the JSON. The entire response must be parseable as a single JSON object.
"""
    
    data = {
        "model": "claude-3-opus-20240229",  # Or use a different model as needed
        "system": system_prompt,
        "messages": [
            {
                "role": "user",
                "content": f"I want to accomplish this in Grasshopper: {prompt}"
            }
        ],
        "max_tokens": 1000
    }
    
    try:
        response = requests.post(url, headers=headers, json=data)
        response.raise_for_status()
        response_data = response.json()
        content = response_data["content"][0]["text"]
        
        # Try to extract and validate JSON from the response
        try:
            # Remove any markdown code block indicators if present
            if "```json" in content and "```" in content:
                content = content.split("```json", 1)[1].split("```", 1)[0].strip()
            elif "```" in content:
                content = content.split("```", 1)[1].split("```", 1)[0].strip()
                
            # Parse the JSON to validate it
            json_data = json.loads(content)
            return {"response": content, "json_data": json_data}
        except json.JSONDecodeError as e:
            return {"response": content, "json_error": str(e)}
            
    except Exception as e:
        return {"error": f"Error calling LLM API: {str(e)}"}

def main():
    # Set up command line arguments
    parser = argparse.ArgumentParser(description='Grasshopper Component Finder')
    parser.add_argument('--prompt', type=str, required=True, help='Natural language prompt describing the task')
    parser.add_argument('--components', type=str, required=True, help='Path to the component database JSON file')
    parser.add_argument('--api-key', type=str, required=True, help='LLM API key')
    parser.add_argument('--output', type=str, help='Output file path (optional, default is stdout)')
    parser.add_argument('--json-only', action='store_true', help='Output only the JSON data')
    
    args = parser.parse_args()
    
    # Load component database
    if not args.json_only:
        print(f"Loading component database from {args.components}...")
    component_data = load_component_database(args.components)
    
    if "error" in component_data:
        error_msg = f"Error: {component_data['error']}"
        if args.json_only:
            error_json = json.dumps({"error": component_data['error']})
            print(error_json)
        else:
            print(error_msg)
        sys.exit(1)
    
    # Call LLM API with the component data
    if not args.json_only:
        print(f"Analyzing prompt: '{args.prompt}'")
    result = call_llm_api(args.prompt, component_data, args.api_key)
    
    if "error" in result:
        error_msg = f"Error: {result['error']}"
        if args.json_only:
            error_json = json.dumps({"error": result['error']})
            print(error_json)
        else:
            print(error_msg)
        sys.exit(1)
    
    # Handle JSON output
    if "json_data" in result:
        json_output = json.dumps(result["json_data"], indent=2)
        
        if args.json_only:
            # Only output the JSON data
            if args.output:
                with open(args.output, 'w') as f:
                    f.write(json_output)
            else:
                print(json_output)
            return
    elif "json_error" in result:
        if not args.json_only:
            print(f"Warning: Could not parse LLM output as JSON: {result['json_error']}")
    
    # Parse the response for human-readable output
    response_text = result["response"]
    
    # If we have valid JSON, format it nicely
    if "json_data" in result:
        json_data = result["json_data"]
        explanation = json_data.get("explanation", "No explanation provided")
        
        components_text = []
        for i, comp in enumerate(json_data.get("components", [])):
            components_text.append(f"{i+1}. {comp.get('name', 'Unknown')} (Category: {comp.get('category', 'Unknown')}, Subcategory: {comp.get('subcategory', 'Unknown')})")
        
        suggested_components = "\n".join(components_text)
        
        connections_text = []
        for conn in json_data.get("connections", []):
            connections_text.append(f"- Connect {conn.get('fromComponent', 'Unknown')} ({conn.get('fromOutput', 'Unknown')}) to {conn.get('toComponent', 'Unknown')} ({conn.get('toInput', 'Unknown')})")
        
        connections = "\n".join(connections_text)
    else:
        # Fallback to simple text parsing
        parts = response_text.split("\n\n", 1)
        if len(parts) > 1:
            suggested_components = parts[0]
            explanation = parts[1]
        else:
            suggested_components = "Component Suggestions:"
            explanation = response_text
        connections = "No connection information available"
    
    # Format the output
    output = f"""
=== GRASSHOPPER COMPONENT FINDER ===

PROMPT:
{args.prompt}

COMPONENT SUGGESTIONS:
{suggested_components}

CONNECTIONS:
{connections}

DETAILED EXPLANATION:
{explanation}
"""
    
    if "json_data" in result:
        output += f"""
JSON DATA:
{json.dumps(result["json_data"], indent=2)}
"""
    
    # Output the result
    if args.output:
        with open(args.output, 'w') as f:
            f.write(output)
        print(f"Results written to {args.output}")
    else:
        print(output)

if __name__ == "__main__":
    main()