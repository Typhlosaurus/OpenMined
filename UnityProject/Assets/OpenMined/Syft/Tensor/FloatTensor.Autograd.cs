﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenMined.Syft.Tensor
{
    public partial class FloatTensor
    {
	    private bool autograd;
	    public FloatTensor Grad { get; private set; }
	    private bool keepgrads;
	    
	    // checks to see if a variable has accumulated all the gradients it should before it backprops
	    public bool AllAutogradChildrenAccountedFor()
	    {
		    for (int i=0; i< children_counts.Count; i++)
		    {
			    if (children_counts[i] == 0 && controller.getTensor(children_indices[i]).autograd)
			    {
				    return false;
			    }
		    }
		    return true;
	    }
	    
	    public void Backward(FloatTensor grad = null, FloatTensor grad_origin = null)
	    {
		    Debug.Log("Backward:" + this.id);
		  
		    if (autograd)
		    {
			    if (grad == null)
			    {
				    Debug.Log("Grad not Found... Creating Gradient of 1s");
				    grad = this.controller.createOnesTensorLike(this);
				    grad.Autograd = false;
			    }

			    if (grad_origin != null)
			    {
				    int child_index = children_indices.IndexOf(grad_origin.Id);
				    if (children_counts[child_index] > 0)
				    {
					    throw new InvalidOperationException("Can't backprop more than once.");
				    }
				    else
				    {
					    children_counts[child_index] += 1;
				    }
			    }

			    if (this.Grad == null)
			    {
				    this.Grad = grad;
				    Debug.Log("Setting Grad Tensor Id:" + this.id);
			    }
			    else
			    {
				    if (this.Grad.id == grad.id)
				    {
					    // do nothing
					    Debug.Log("Not Updating For Tensor Id:" + this.id);
				    }
				    else
				    {
					    Debug.Log("Updating For Tensor Id:" + this.id);
					    this.Grad.Zero_();
					    this.Grad.Add(grad, inline: true);
				    }

			    }

			    // grads must not have grads of their own
			    if (this.Grad.autograd == true)
			    {
				    throw new InvalidOperationException("Sorry, grads cannot have grads");
			    }
			    
			  
			    // only continue backpropping if there's something to backprop into
			    // only continue backpropping if all gradients (from children) are accounted for
			    // override waiting for children if "backprop" was called on this variable directly
			    
			    
				// RULES FOR AUTOGRAD:
			    // 1) if you need to use "this" for calculating a gradient, copy it first and set autograd to false (see sigmoid)
			    // 2) if you use a method in your backprop logic that doesn't hook into the dynamic graph yet, backprop
			    // will not work!!! Make sure there's a "hookautograd" function in every method you use for backprop.
			    // 3) whenever backpropping into a method where the forward prop involved a scalar (such as scalar
			    // multiplication), current implementations assume you will NOT backprop into the scalar itself.
			    if (this.creators != null && this.creators.Count > 0 && (grad_origin == null || AllAutogradChildrenAccountedFor()))
			    {
				    
				    if (creation_op == "add_elem")
				    {

					    controller.getTensor(creators[0]).Backward(grad, this);
					    controller.getTensor(creators[1]).Backward(grad, this);

				    }
				    else if (creation_op == "add_scalar")
				    {
					    controller.getTensor(creators[0]).Backward(grad, this);
				    }
				    else if (creation_op == "copy")
				    {
					    controller.getTensor(creators[0]).Backward(grad, this);
				    }
				    else if (creation_op == "div_elem")
				    {
					    FloatTensor x = controller.getTensor(creators[0]);
					    FloatTensor y = controller.getTensor(creators[1]);

					    x.Backward(grad.Div(y));

					    FloatTensor y2 = y.Pow(2);
					    FloatTensor xn = x.Neg();
					    FloatTensor xny2 = xn.Div(y2);
					    FloatTensor gradxny2 = grad.Mul(xny2);
					    y.Backward(gradxny2);
				    }
				    else if (creation_op == "div_scalar")
				    {
					    controller.getTensor(creators[0]).Backward(grad.Div(controller.getTensor(creators[1]).data[0]), this);
				    }
				    else if (creation_op == "mul_elem")
				    {
					    controller.getTensor(creators[0]).Backward(grad.Mul(controller.getTensor(creators[1])), this);
					    controller.getTensor(creators[1]).Backward(grad.Mul(controller.getTensor(creators[0])), this);
				    }
				    else if (creation_op == "mul_scalar")
				    {
					    controller.getTensor(creators[0]).Backward(grad.Mul(controller.getTensor(creators[1]).data[0]), this);
				    }
				    else if (creation_op == "mm")
				    {
					    controller.getTensor(creators[0]).Backward(grad.MM(controller.getTensor(creators[1]).Transpose()), this);
					    controller.getTensor(creators[1]).Backward(controller.getTensor(creators[0]).Transpose().MM(grad), this);
				    }
				    else if (creation_op == "neg")
				    {
					    controller.getTensor(creators[0]).Backward(grad.Neg(), this);
				    }
				    else if (creation_op == "pow_scalar")
				    {
					    FloatTensor x = controller.getTensor(creators[0]);
					    x.Backward(x.Mul(grad).Mul(controller.getTensor(creators[1]).Data[0]), this);
				    }
				    else if (creation_op == "sub_elem")
				    {
					    controller.getTensor(creators[0]).Backward(grad.Copy(), this);
					    controller.getTensor(creators[1]).Backward(grad.Neg(), this);
				    }
				    else if (creation_op == "sub_scalar")
				    {
					    controller.getTensor(creators[0]).Backward(grad, this);
				    }
				    else if (creation_op == "sigmoid")
				    {
					    FloatTensor self_nograd = this.Copy();
					    self_nograd.autograd = false;
					    
					    controller.getTensor(creators[0]).Backward(self_nograd.Neg().Add((float) 1).Mul(self_nograd).Mul(grad), this);
				    }
				    else if (creation_op == "transpose")
				    {
					    controller.getTensor(creators[0]).Backward(grad.Transpose());
				    }
				    else
				    {
					    //Debug.Log("Autograd couldn't find matching operation for:" + creation_op);   
				    }
			    }
		    }
		    else
		    {
			    Debug.Log("Autograd off - skipping backprop...");
		    }
	    }
    }
}