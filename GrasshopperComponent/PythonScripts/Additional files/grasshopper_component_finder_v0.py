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

Based on these components, suggest the best way to accomplish the user's task.
First, explain the overall approach (what geometric operations are needed).
Then, list the specific components needed in order of use, with:
1. Component name and where to find it (category/subcategory)
2. How to connect it to other components (which inputs/outputs to use)
3. Any special settings or parameters to adjust

Your explanation should be clear enough for someone with basic Grasshopper knowledge to follow.
If the user's request requires components not listed above, mention that but still provide the best
possible solution using the available components.
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
        return {"response": content}
    except Exception as e:
        return {"error": f"Error calling LLM API: {str(e)}"}

def main():
    # Set up command line arguments
    parser = argparse.ArgumentParser(description='Grasshopper Component Finder')
    parser.add_argument('--prompt', type=str, required=True, help='Natural language prompt describing the task')
    parser.add_argument('--components', type=str, required=True, help='Path to the component database JSON file')
    parser.add_argument('--api-key', type=str, required=True, help='LLM API key')
    parser.add_argument('--output', type=str, help='Output file path (optional, default is stdout)')
    
    args = parser.parse_args()
    
    # Load component database
    print(f"Loading component database from {args.components}...")
    component_data = load_component_database(args.components)
    
    if "error" in component_data:
        print(f"Error: {component_data['error']}")
        sys.exit(1)
    
    # Call LLM API with the component data
    print(f"Analyzing prompt: '{args.prompt}'")
    result = call_llm_api(args.prompt, component_data, args.api_key)
    
    if "error" in result:
        print(f"Error: {result['error']}")
        sys.exit(1)
    
    # Parse the response
    response_text = result["response"]
    parts = response_text.split("\n\n", 1)
    
    if len(parts) > 1:
        suggested_components = parts[0]
        explanation = parts[1]
    else:
        suggested_components = "Component Suggestions:"
        explanation = response_text
    
    # Format the output
    output = f"""
=== GRASSHOPPER COMPONENT FINDER ===

PROMPT:
{args.prompt}

COMPONENT SUGGESTIONS:
{suggested_components}

DETAILED EXPLANATION:
{explanation}
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